# Findings

## Sources
- Steam store: official online/local co-op cap is **4**.
- [LargerLobbyMod on Thunderstore](https://thunderstore.io/c/wobbly-life/p/TitusXRyu/LargerLobbyMod/) (v1.1.0, TitusXRyu) –
  proves a 100-player host-only lobby is possible. It targets the **Xbox app / Game Pass** build (BepInEx 6 IL2CPP) and
  states it does **not** work on Steam. No license or source repo is published, so we did **not** copy code; we only read
  public assembly metadata (type/method names it references) to learn the game's structure, then wrote our own.
- Its authors note play with >4 players is lightly tested, first joins sometimes fail, and joins can hang after many
  players come and go (restart the world).

## Game structure (from the Game Pass build's metadata – Steam build still to be confirmed)
- Networking lives in namespace `HawkNetworking`:
  - `HawkNetworkManager` – base. Members used by mods: `GetMaxPlayerCount`, `GetCurrentPlayerCount`, `GetPlayers`, `IsJoinable`, `IsServer`, `DefaultInstance`.
  - Platform subclasses: `EOSNetworkManager` (Epic Online Services, used for crossplay), `GameCoreNetworkManager` (Xbox/GameCore). Each has `HostServer(...)`.
  - `HawkConnection` / `EOSConnection` – per-player connection objects.
- Game layer: `WobblyNetworkManager` (`KickPlayer`), `PlayerController`, `PlayerCharacter`, `GameInstance`, `LocalPlayerManager`.
- Existing mod technique: Harmony prefix on each `HostServer` raising the max-players argument ("only ever raises the game's value"), plus logging hooks. One of its notes: patching a force-disconnect method crashes the game at the splash screen – avoid.

## Open questions (resolve on the Steam build)
1. Is the Steam build Mono or IL2CPP? (decides BepInEx 5 vs 6)
2. Is there a Steam-specific manager subclass (name?), and does it have `HostServer` with a max-players parameter, or is Steam lobby size set elsewhere (`ISteamMatchmaking.CreateLobby(maxMembers)` caps at 250)?
3. Does the Steam build also route through EOS for crossplay (then the EOS lobby `MaxLobbyMembers` also matters)?
4. Any hard-coded 4-player assumptions: spawn points, UI slot arrays, split-screen, mission scripts.

## Planned next steps
- Run the plugin with diagnostics on the Steam build; fix targets from the log.
- Test with 5-8 players, then scale up (bots/alt clients) while watching host CPU, bandwidth and desync.
- Add mitigations if needed: lower tick/send rates, disable per-player cosmetic sync, cap physics-object interactions.
