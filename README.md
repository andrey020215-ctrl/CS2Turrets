# CS2Turrets — CounterStrikeSharp

> **Preview / experimental implementation.** Compiled successfully on GitHub Actions ([visual model build #5](https://github.com/andrey020215-ctrl/CS2Turrets/actions/runs/37745105109)) against CounterStrikeSharp.API 1.0.376 and .NET 10. **Live CS2 gameplay has not been tested.** Test on a private server before production. The `Damage` routine directly changes HP; kills use `CommitSuicide` and do not credit the turret owner.

## Gameplay

- G (default `drop`) opens a CounterStrikeSharp HTML menu; choose Rapid or Heavy turret.
- Aim at a nearby surface and press G again to place the selected type. Use `!turret_place` as an alternative.
- Approach one of your own turrets and press E (`+use`) to upgrade it, maximum level 3.
- Enemy players in range are targeted automatically if line of sight is clear.
- Owner/team limits, minimum spacing, and upgrades are configurable.
- Turrets are removed when the round begins, the owner disconnects, or the map changes.

## Server requirements

- Dedicated CS2 server with [Metamod:Source](https://www.sourcemm.net/) and a matching [CounterStrikeSharp](https://docs.cssharp.dev/) release.
- .NET runtime required by the CounterStrikeSharp release.
- A model path actually available on your CS2 server (see caveat below).

## Build

```bash
dotnet restore CS2Turrets/CS2Turrets.csproj
dotnet build CS2Turrets/CS2Turrets.csproj -c Release
```

The repository includes a GitHub Actions build that attempts the same build and publishes `CS2Turrets-server.zip` **only if compilation succeeds**. On GitHub open *Actions → Build CS2Turrets → most recent run → Artifacts*. Do not treat a pushed source commit as a successful compilation.

## Install

If the workflow succeeds, extract `CS2Turrets-server.zip` into `game/csgo/` (or your game content root), preserving `addons/counterstrikesharp/`. Alternatively copy:

```text
CS2Turrets.dll -> game/csgo/addons/counterstrikesharp/plugins/CS2Turrets/CS2Turrets.dll
CS2Turrets.json -> game/csgo/addons/counterstrikesharp/configs/plugins/CS2Turrets/CS2Turrets.json
```

Restart the server (or use the CounterStrikeSharp reload command), then check server console for load errors.

**Visible composite model (v0.2.0-preview):** The plugin now spawns *two separate existing CS2 models* at different heights — a supply crate base and an M249 machine gun above it. This is a gameplay visual assembled from existing assets, **not a newly authored Source 2 .vmdl_c**. These paths are references to the installed CS2 game resources, not files provided in this repository. The resource paths are `models/props/crates/csgo_drop_crate_dangerzone.vmdl` and `weapons/models/m249/weapon_mach_m249.vmdl`. If either model does not render, inspect server logs and ensure resources are available; adjust `BaseOffsetZ`, `GunOffsetZ`, `GunOffsetForward`, and `GunPitch` to refine alignment.

Set `UseCompositeVisual` to `false` and set `Model` to a verified model path to use a single model instead. Runtime spawn/visibility on a dedicated CS2 server **has not yet been verified**.

## Commands

| Command | Action |
| --- | --- |
| `G` (bound to `drop`) | Open turret menu / confirm placement |
| `!turret` | Open turret selection |
| `!turret_place` | Confirm current placement |
| `!turret_cancel` | Cancel current placement |
| `E` (`+use`) | Upgrade your nearby turret |

To avoid stealing default weapon drop, set `InterceptDropKey` to `false` and bind an alternative command (e.g. `bind g "css_turret"`). The server may need to support the command being bound as a client console command. Menu navigation depends on CounterStrikeSharp's current menu controls.

## Known issues / work needed before production

1. GitHub Actions build #4 passed with a DLL and installable ZIP; actual gameplay and server load are not verified. CS2 and CounterStrikeSharp change often.
2. Trace and model spawning need runtime testing on multiple maps, especially sloped surfaces, collision, visibility, and prop precaching.
3. Damage is not using Source 2's engine damage system, so kills and hit feedback/score attribution are not correct.
4. The crate-and-M249 composite does not rotate or animate toward targets; no visible projectiles or muzzle flash in this preview.
5. G hook intercepts normal weapon dropping and E upgrades can overlap map `+use` interactions.
6. The internal timer approximates 64 ticks/sec; game ticks can differ. Use the server time API when verified.
7. This is not a production-ready install-and-play plugin until dedicated-server tests pass; compilation and ZIP packaging now succeed.

## Licensing

Project code authored for this repository. Third-party CounterStrikeSharp and game assets retain their licenses.
