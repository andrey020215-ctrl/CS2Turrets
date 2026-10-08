# CS2Turrets — CounterStrikeSharp 0.3.0-preview

Counter-Strike 2 server plugin: use **G** (default `drop`) to open the turret menu, aim at flat ground, press **G** again to install, and press **E** (`+use`) near your turret to upgrade.

## Four levels

| Level | Behavior |
| --- | --- |
| 1 | Regular turret, automatically attacks nearby enemies |
| 2 | Bullet damage and range increase |
| 3 | Further bullet damage and range increase |
| **4** | **Rocket launcher appears above the turret. The turret fires actual moving, visible server-simulated rocket projectiles instead of instant bullets, which explode with splash damage.** |

Level four spawns two additional stock CS2 props: a **Negev** gun model as an upper launcher-like module and a **HE grenade** mesh as the loaded warhead. The flying rocket uses that same HE mesh. These are **temporary stock-game placeholders**, NOT a true custom missile or unique 3D model. Their runtime visibility is not yet verified. Set `RocketLauncherModel` and `RocketVisualModel` to paths of custom compiled `.vmdl_c` resources (without the `_c` suffix) when your real model package is ready.

### Rocket parameters

| Setting | Default | Meaning |
| --- | --- | --- |
| `MaxLevel` | 4 | Last upgrade |
| `RocketLevel` | 4 | Level at which rockets activate |
| `RocketDamage` | 90 | Maximum damage at explosion center |
| `RocketRadius` | 145 | Splash radius in game units |
| `RocketSpeed` | 800 | Movement speed in units/second |
| `RocketFireInterval` | 2.5 | Seconds between rocket launches |
| `RocketLifeSeconds` | 3 | Maximum rocket lifetime |
| `RocketHitRadius` | 36 | Hit tolerance to enemy player's center |
| `MaxActiveRockets` | 24 | Maximum active rockets on the server |
| `RocketFriendlyFire` | false | Whether explosions damage allies |
| `EnableRocketExplosionEffects` | true | Precache and show a small-lived impact effect |
| `RocketExplosionParticle` | `particles/explosions_fx/explosion_c4_short.vpcf` | Built-in explosion effect |
| `ExplosionEffectLifetime` | 0.7 | Seconds before effect entity is removed |

The rocket's direction is determined at launch. It does **not** home in after launch. Each tick performs a short map collision trace and checks nearby players. On impact, splash damage falls off with distance. Visual projectiles are cleaned up when destroyed, expired, disconnected, or when a round or map changes. Rocket counts and effect lifetimes are bounded.

## Requirements

- CS2 dedicated game server with [Metamod:Source](https://www.sourcemm.net/) and [CounterStrikeSharp](https://docs.cssharp.dev/), compatible with CounterStrikeSharp.API 1.0.376.
- Required .NET runtime provided by your CounterStrikeSharp server installation.

## Download/build

[GitHub Actions: Build CS2Turrets](https://github.com/andrey020215-ctrl/CS2Turrets/actions/workflows/build.yml)

Select the **latest successful main branch run**, then download the **CS2Turrets-server** artifact. It contains an installable ZIP (`CS2Turrets-server.zip`) with the DLL and default JSON configuration. A successful GitHub Actions run confirms only compilation and packaging — **not gameplay testing**.

To build from source:

```bash
dotnet restore CS2Turrets/CS2Turrets.csproj
dotnet build CS2Turrets/CS2Turrets.csproj -c Release
```

To install, extract the installable ZIP into your server's `game/csgo/` root:

```text
game/csgo/addons/counterstrikesharp/plugins/CS2Turrets/CS2Turrets.dll
game/csgo/addons/counterstrikesharp/configs/plugins/CS2Turrets/CS2Turrets.json
```

Restart server. **Change the map after enabling the rocket particle effect**, because particle precaching runs at map startup. Do not install the preview on a production server until you've tested it on a local/private CS2 server.

## Commands

| Command/key | Function |
| --- | --- |
| G (bound to `drop`) | Open menu / confirm turret installation |
| `!turret` | Open turret menu |
| `!turret_place` | Place selected turret |
| `!turret_cancel` | Cancel placement |
| E (bound to `+use`) | Upgrade nearest personally owned turret, up to level 4 |

By default, the plugin intercepts normal weapon dropping for G. Set `InterceptDropKey` to `false` to disable interception and choose another binding. E can conflict with ordinary map `+use` interactions. Read and tune your JSON settings according to your server.

## Limitations requiring game-server tests

1. **No dedicated-server gameplay validation** yet. Verify entity spawning, model precaching, maps, player hit tracking, and round cleanup.
2. The upper launcher and flying warhead are *stock asset placeholders*; actual compiled custom missile models and moving launcher animations are still missing.
3. Player damage directly reduces health. Fatal blows currently invoke `CommitSuicide`; **killer attribution, armor and killfeed may not reflect the turret owner**.
4. Rocket explosions show a precached particle, but there is no custom sound, rocket trail or sprite flame yet.
5. Rocket movement and model collision depend on CS2 engine behaviour and may require further tuning after testing.
6. Timer and entity limits bound server work but have not been FPS-benchmarked on a populated server.

## Licensing

Only plugin source code and configuration are included. Valve game models are referenced by resource path and are not redistributed. CounterStrikeSharp retains its respective license.
