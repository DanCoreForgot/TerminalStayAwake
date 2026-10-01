# TerminalStayAwake

A GTFO mod that keeps a terminal awake when you walk away from it, so a running command keeps going.
It does the same thing as the vanilla "hold Q while walking away" trick, without having to do the trick.

- Walk away from an awake terminal and it stays lit and keeps processing.
- At most `MaxAwakeTerminals` terminals (default 2) are kept awake. Keeping one more puts the oldest back to sleep.
- A kept terminal is not put to sleep while another player is standing near it.
- Coming back to a kept terminal works as normal.

## Client-side

Works whether you are host or client. **Nobody else in the lobby needs the mod.**
The mod adds no network messages of its own. It only skips the "go to sleep" request your game would normally send
when you walk away, and the over-limit sleep is the same request vanilla sends.
Terminal state is shared, so everyone in the lobby sees a terminal you kept as awake.

Known limitation: if a teammate without the mod walks up to a terminal you kept awake and walks away again,
their game puts it to sleep the vanilla way.

## Requirements

- GTFO (built and checked against Steam revision 34873)
- BepInEx 6 IL2CPP (BepInExPack for GTFO, e.g. via r2modman)
- [GTFO-API](https://thunderstore.io/c/gtfo/p/GTFOModding/GTFO_API/) (`dev.gtfomodding.gtfo-api`)

## Install

1. Get `TerminalStayAwake.dll` from [`build/`](build/TerminalStayAwake.dll) or the Releases page.
2. Put it in `BepInEx/plugins/TerminalStayAwake/` of your r2modman profile
   (`%AppData%\r2modmanPlus-local\GTFO\profiles\<profile>\BepInEx`).
   In r2modman you can also use Settings > Import local mod.
3. Launch modded.

Uninstall: delete the DLL (or disable it in r2modman).

## Config

`BepInEx/config/sourmonkis.TerminalStayAwake.cfg`, created on first launch:

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Turn the mod on or off. |
| `MaxAwakeTerminals` | `2` | How many terminals may stay awake after you left them (1-8). |

## Troubleshooting

Check `BepInEx/LogOutput.log` in the profile for `TerminalStayAwake 0.2.0 loaded`.
If it says `Patching failed`, the game update probably changed the terminal code; the mod then stays inactive
and the game runs normally. Per-terminal messages are logged at Debug level
(enable `Debug` under `[Logging.Console]` / `[Logging.Disk]` `LogLevels` in `BepInEx/config/BepInEx.cfg`).

## Build

Needs the .NET SDK (6.0 or newer) and an r2modman GTFO profile that has been launched once
(for BepInEx core, the generated interop assemblies and GTFO-API).

```
dotnet build -c Release -o build -p:BepInExDir="%AppData%\r2modmanPlus-local\GTFO\profiles\<profile>\BepInEx"
```

How the game's terminal sleep logic works, and why this is client-safe, is written up in [MODLOG.md](MODLOG.md).

## Changelog

- **0.2.0**: Don't put a terminal to sleep on a nearby teammate; guard stored terminal references;
  a patch failure no longer affects game load. Documented client-side behaviour.
- **0.1.0**: First version.

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx), [HarmonyX](https://github.com/BepInEx/HarmonyX) and
  [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop)
- [GTFO-API](https://github.com/GTFO-Modding/GTFO-API) for level events
- Built with [Claude Code](https://claude.com/claude-code)

GTFO is made by 10 Chambers. This mod is not affiliated with or endorsed by 10 Chambers.

## License

[MIT](LICENSE)
