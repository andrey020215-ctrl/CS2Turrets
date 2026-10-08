# CS2Turrets original 3D asset workflow

The 3D authoring package delivered with this project includes original editable GLB meshes for:

- `turret_levels_1_3.glb` — base and machine-gun head;
- `turret_level_4_rocket.glb` — complete fourth-level turret with four rocket tubes;
- `turret_base_static.glb` — static base;
- `turret_gun_head.glb` — standalone rotating section;
- `turret_quad_rocket_pod.glb` — four-tube rocket head;
- `turret_rocket_projectile.glb` — separate guided-looking but unguided game missile shape.

The model package also contains mesh generator source (Python + trimesh), OBJ exports, true-mesh renders and a Blender FBX conversion script.

**These are editable MODEL SOURCES, not compiled Source 2 resources.** Model binary assets are not part of this repository's DLL package. Deploying the plugin alone cannot deliver them to players.

## Prepare for Source 2

1. Import the GLB files into Blender. The procedural models are oriented **+X forward, +Z up**, in meters.
2. Export to FBX with matching axes; as needed scale by about `39.370079` to convert meters to Source 2 inches. Confirm final size in ModelDoc.
3. Open CS2 Workshop Tools / ModelDoc to import and compile 3 models and corresponding materials:
   - `models/cs2turrets/turret_lv1.vmdl` from `turret_levels_1_3`
   - `models/cs2turrets/turret_lv4.vmdl` from `turret_level_4_rocket`
   - `models/cs2turrets/rocket.vmdl` from `turret_rocket_projectile`
4. Distribute **compiled models/materials** through a CS2 addon that both clients and server load.
5. Only then set `UseCustomTurretModel` to `true` in `addons/counterstrikesharp/configs/plugins/CS2Turrets/CS2Turrets.json`. Ensure all three custom paths match your compiled resource paths.
6. Restart and change the map, then test placements, level-4 model swaps, missile visuals, hit effects and client downloads.

The plugin retains `UseCustomTurretModel=false` by default so it can still run with its older built-in game-prop placeholders. The GLB source files are **not** dynamically loaded by CounterStrikeSharp, and the DLL compiling successfully is not a test that models display in CS2.

Animated rotation, launch recoil and reload timing still require a Source 2 animation setup and corresponding plugin control; the supplied GLB meshes are static but have separately exportable subassemblies.
