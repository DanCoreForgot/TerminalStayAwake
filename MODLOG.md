# MODLOG — TerminalStayAwake (GTFO)

## Goal
QoL, client-side (works as host or client, nobody else needs it): a terminal stays awake when the player walks away, without the hold-Q trick.
At most N terminals (config, default 2) are kept awake; keeping one more puts the oldest to sleep.
Done = works in game (terminal screen stays lit and keeps processing after walking away; limit enforced).

## Paths
- Game: `<Steam library>\steamapps\common\GTFO` (Steam 493520, revision 34873, Unity 2019.4.21f1 IL2CPP, no anti-cheat)
- Saves/logs: `%USERPROFILE%\AppData\LocalLow\10 Chambers Collective\GTFO`
- Loader: BepInEx 6.0.0-be.665 via r2modman, profiles in `%AppData%\r2modmanPlus-local\GTFO\profiles\<name>\BepInEx`
  (game folder itself only has doorstop files). GTFO-API 0.5.0 (`dev.gtfomodding.gtfo-api`) in every profile.
- Build references: an r2modman profile (default name `Singulate` in the .csproj; core, interop, GTFO-API). Override with `-p:BepInExDir=...`.
- Decomp (outside this folder, never commit): `~/gtfo-decomp`
  - `isil/` Cpp2IL ISIL dump, `diffable/` field offsets, `tools/Cpp2IL.exe`, `tools/isil.py <Class> [method]`
  - ilspycmd 9.1.0.7988 installed as a dotnet global tool (latest needs .NET 10)

## Existing mods checked
- Thunderstore `Daem0n/BetterTerminal` 1.1.3: terminal UX only, does not touch sleep behaviour.

## How it works in the game (read from ISIL, revision 34873)
- `LG_ComputerTerminal : StateMachine<LG_TERM_Base>`; states in `TERM_State` (Sleeping=0, Awake=1, PlayerInteracting=2, ... PasswordProtected=12).
- `LG_TERM_Sleeping.Enter`: hides text, and `m_terminal.enabled = false` unless it has an UplinkPuzzle. A sleeping
  terminal does not run `Update`, which is why commands stall when you leave.
- `LG_TERM_Sleeping.OnProximityEnter` -> `ChangeState(Awake)` (or PasswordProtected). `LG_TERM_Awake.Enter` sets `enabled = true`.
- `LG_TERM_Awake.OnProximityExit` -> `ChangeState(Sleeping)`. Only Awake and PasswordProtected override OnProximityExit.
- `LG_ComputerTerminal.ChangeState` -> `LG_ComputerTerminalManager.WantToChangeTerminalState` (networked state).
- Call chain: `PlayerInteraction.UpdateWorldInteractions` -> `RemoveFromProximity(interact)` ->
  `Interact_ComputerTerminal.OnProximityExit` -> `LG_ComputerTerminal.OnProximityExit` -> current state.
- **Cause of the hold-Q trick:** Q = `ToggleCommunicationMenu`. `UpdateWorldInteractions` returns early unless
  `FocusStateManager.CurrentState == 4` (FPS). Its proximity pass only looks at colliders from the current sphere
  search, and calls RemoveFromProximity for ones beyond `m_proximityRadius`. Leave the search sphere while the
  comms menu is open and the terminal is never in a pass again, so the exit never fires and it stays Awake.
  Coming back and leaving normally fires it.

## Route
Managed-code patching: BepInEx IL2CPP plugin + Harmony, hard dependency on GTFO-API. No native hooks, no input injection.
- Prefix `LG_ComputerTerminal.OnProximityExit`: if state is Awake, skip the original, remember the terminal;
  over the limit -> oldest gets `ChangeState(Sleeping)` (the vanilla path).
- Postfix `LG_ComputerTerminal.OnProximityEnter`: player is back, stop counting that terminal.
- List cleared on `LevelAPI.OnLevelCleanup` / `OnBuildStart`.
- Other states (PasswordProtected etc.) are left vanilla.

## Status
- 2026-09-30: builds clean -> `build/TerminalStayAwake.dll`. NOT yet installed into a profile, NOT yet tested in game.
- 2026-10-01: 0.2.0 hardening + client-side review. Builds clean. Still NOT tested in game.
  - Confirmed client-side safe: state change = `SNet_AuthorativeAction<pTerminalState>.Ask`; host validation only checks
    the terminal ID is registered, then broadcasts. Clients may send it; skipping our own Sleeping request = hold-Q trick.
    `PlayerInteraction.Update` only runs world interactions for the locally owned agent, so the patch never sees remote players.
  - Over-limit sleep skips a terminal if another real (non-bot, alive) player is within 4 m, so we never sleep it on a teammate.
  - Alive/WasCollected guards on stored wrappers; dead entries pruned; PatchAll wrapped so a game update can't crash load.
- 2026-10-01: 0.2.0 installed into profile `GTFOmodding`, replacing the 0.1.0 r2modman import at
  `BepInEx/plugins/uhh-TerminalStayAwake.dll/TerminalStayAwake.dll` (a folder; r2modman manifest says 1.0.0). Not yet launched.

## Next
- Copy the DLL to `<profile>\BepInEx\plugins\TerminalStayAwake\`, launch modded from r2modman.
- Check `BepInEx/LogOutput.log` for "TerminalStayAwake 0.1.0 loaded" and no Harmony errors.
- In a level: wake terminal A, walk away -> screen stays on. Repeat past the limit -> oldest sleeps.
  Debug lines need `LogLevels` to include Debug in `BepInEx.cfg`.
- Config: `BepInEx/config/sourmonkis.TerminalStayAwake.cfg`.

## Notes
- Terminal state is networked: in co-op everyone sees a kept terminal as awake (same as the vanilla hold-Q trick).
- A vanilla teammate who walks up to a kept terminal and away again will put it to sleep (their client sends the vanilla request).
