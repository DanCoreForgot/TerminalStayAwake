using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using GTFO.API;
using HarmonyLib;
using LevelGeneration;
using Player;
using UnityEngine;

namespace TerminalStayAwake;

[BepInPlugin(GUID, NAME, VERSION)]
[BepInDependency("dev.gtfomodding.gtfo-api", BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BasePlugin
{
    public const string GUID = "sourmonkis.TerminalStayAwake";
    public const string NAME = "TerminalStayAwake";
    public const string VERSION = "0.2.0";

    internal static ManualLogSource L;
    internal static ConfigEntry<bool> Enabled;
    internal static ConfigEntry<int> MaxAwake;

    public override void Load()
    {
        L = Log;
        Enabled = Config.Bind("General", "Enabled", true,
            "Keep terminals awake when you walk away from them. Client-side: works as host or client, nobody else needs the mod.");
        MaxAwake = Config.Bind("General", "MaxAwakeTerminals", 2,
            new ConfigDescription(
                "How many terminals may stay awake at once after you left them. When one more is kept, the oldest goes to sleep.",
                new AcceptableValueRange<int>(1, 8)));

        LevelAPI.OnLevelCleanup += KeepAwake.Clear;
        LevelAPI.OnBuildStart += KeepAwake.Clear;

        try
        {
            new Harmony(GUID).PatchAll(typeof(Plugin).Assembly);
        }
        catch (Exception e)
        {
            // A game update that renames a patched method must not take the game down; the mod just does nothing.
            Log.LogError($"Patching failed, {NAME} is inactive: {e}");
            return;
        }
        Log.LogInfo($"{NAME} {VERSION} loaded (max awake: {MaxAwake.Value})");
    }
}

// Vanilla: PlayerInteraction (local player only) -> Interact_ComputerTerminal.OnProximityExit -> LG_ComputerTerminal.OnProximityExit
// -> current state's OnProximityExit. Only LG_TERM_Awake (and PasswordProtected) react, by changing to Sleeping,
// which disables the terminal behaviour so it stops updating. Holding the comms menu key while walking away makes
// PlayerInteraction skip its proximity pass, so that call never arrives. We drop the call on purpose instead.
//
// Client-side: ChangeState -> LG_ComputerTerminalManager.WantToChangeTerminalState -> SNet_AuthorativeAction.Ask,
// which any client may send and the host applies without extra checks. Not sending the Sleeping request is exactly
// what the hold-Q trick does, and putting the oldest to sleep is the same request vanilla sends on walking away.
// No packets of our own, so the host and the other players do not need the mod.
internal static class KeepAwake
{
    // Another real player this close to a terminal is probably using it, so we do not put it to sleep on them.
    private const float OtherPlayerRadius = 4f;

    private static readonly List<LG_ComputerTerminal> s_kept = new();

    internal static void Clear() => s_kept.Clear();

    private static bool Alive(LG_ComputerTerminal t)
    {
        try { return t != null && !t.WasCollected && t.Pointer != IntPtr.Zero && t.gameObject != null; }
        catch { return false; }
    }

    private static bool Same(LG_ComputerTerminal a, LG_ComputerTerminal b)
    {
        try { return a != null && !a.WasCollected && a.Pointer == b.Pointer; }
        catch { return false; }
    }

    private static void Remove(LG_ComputerTerminal t) => s_kept.RemoveAll(k => !Alive(k) || Same(k, t));

    private static bool OtherPlayerNear(LG_ComputerTerminal t)
    {
        var players = PlayerManager.PlayerAgentsInLevel;
        if (players == null)
            return false;

        Vector3 pos = t.transform.position;
        float r2 = OtherPlayerRadius * OtherPlayerRadius;
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            if (p == null || p.IsLocallyOwned || !p.Alive)
                continue;
            var owner = p.Owner;
            if (owner == null || owner.IsBot)
                continue;
            if ((p.Position - pos).sqrMagnitude <= r2)
                return true;
        }
        return false;
    }

    private static void Sleep(LG_ComputerTerminal t)
    {
        if (!Alive(t) || t.CurrentStateName != TERM_State.Awake)
            return;
        if (OtherPlayerNear(t))
        {
            // Leave it to them; their own walk-away sleeps it the vanilla way.
            Plugin.L.LogDebug($"Terminal {t.m_serialNumber} released without sleeping, another player is near");
            return;
        }
        Plugin.L.LogDebug($"Terminal {t.m_serialNumber} put to sleep (limit {Plugin.MaxAwake.Value})");
        t.ChangeState(TERM_State.Sleeping);
    }

    // True when the terminal should ignore this proximity exit.
    internal static bool TryKeep(LG_ComputerTerminal t)
    {
        if (!Plugin.Enabled.Value || !Alive(t) || t.CurrentStateName != TERM_State.Awake)
            return false;

        Remove(t);
        s_kept.Add(t);

        while (s_kept.Count > Plugin.MaxAwake.Value)
        {
            var oldest = s_kept[0];
            s_kept.RemoveAt(0);
            try { Sleep(oldest); }
            catch (Exception e) { Plugin.L.LogError(e); }
        }

        Plugin.L.LogDebug($"Terminal {t.m_serialNumber} kept awake ({s_kept.Count}/{Plugin.MaxAwake.Value})");
        return true;
    }

    // Player is back at the terminal, so it is awake the normal way and no longer counts towards the limit.
    internal static void Release(LG_ComputerTerminal t) => Remove(t);
}

[HarmonyPatch(typeof(LG_ComputerTerminal), nameof(LG_ComputerTerminal.OnProximityExit))]
internal static class Patch_OnProximityExit
{
    private static bool Prefix(LG_ComputerTerminal __instance)
    {
        try { return !KeepAwake.TryKeep(__instance); }
        catch (Exception e) { Plugin.L.LogError(e); return true; }
    }
}

[HarmonyPatch(typeof(LG_ComputerTerminal), nameof(LG_ComputerTerminal.OnProximityEnter))]
internal static class Patch_OnProximityEnter
{
    private static void Postfix(LG_ComputerTerminal __instance)
    {
        try { KeepAwake.Release(__instance); }
        catch (Exception e) { Plugin.L.LogError(e); }
    }
}
