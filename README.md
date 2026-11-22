# Wobbly Life Mods

Mods for [Wobbly Life](https://store.steampowered.com/app/1211020/Wobbly_Life/) (Steam build).

| Mod | Status | What it does |
|-----|--------|--------------|
| [HundredPlayerWorld](src/HundredPlayerWorld) | **Untested scaffold** – compiles, not yet run in game | Raises the online lobby cap from 4 to 100 (configurable) when you host. Guests need nothing installed. |

> ⚠️ 100 players is far beyond what the game was designed for (official cap is 4). Expect desyncs, missions/jobs
> that assume ≤4 players, and host performance problems. Use a fresh world and keep backups.

## Quick start

1. Install **BepInEx 5 (x64, Mono)** into the Steam game folder, run the game once, quit.
   *Confirm the game is Mono first:* `Wobbly Life_Data/Managed/Assembly-CSharp.dll` should exist. If you only find
   `GameAssembly.dll`, the Steam build is IL2CPP and you need BepInEx 6 IL2CPP instead (the csproj and plugin base class change).
2. Build: `cd src/HundredPlayerWorld && dotnet build -c Release`
3. Copy `bin/Release/net472/HundredPlayerWorld.dll` to `<game>/BepInEx/plugins/`.
4. Host a world, then read `BepInEx/LogOutput.log`. With `DiagnosticsMode = true` (default) it prints every network
   manager type and `HostServer` / `GetMaxPlayerCount` signature it found and what it changed.

Config: `BepInEx/config/local.wobblylife.hundredplayerworld.cfg` (`MaxPlayers`, `DiagnosticsMode`, `MaxArgNameContains`).

## Finding the patch targets (if the log says nothing was patched)

```bash
pip install dnfile
python3 -I tools/inspect_assembly.py "<game>/Wobbly Life_Data/Managed/Assembly-CSharp.dll" \
  --type-filter NetworkManager --method-filter HostServer --method-filter MaxPlayer --method-filter Joinable
```

Then update `BaseTypeName` / method names in `src/HundredPlayerWorld/Plugin.cs`.
See [docs/FINDINGS.md](docs/FINDINGS.md) for what is known and what is still a guess.

## Repo layout

- `src/HundredPlayerWorld/` – BepInEx 5 plugin (reflection-only; builds without game DLLs)
- `tools/inspect_assembly.py` – metadata dumper for locating game classes/methods
- `docs/FINDINGS.md` – research notes

Do not commit game files or other people's mod binaries (see `.gitignore`).
