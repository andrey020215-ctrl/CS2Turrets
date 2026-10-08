"""Configuration smoke checks. These do not substitute for a live CS2 test."""
from pathlib import Path
import json

root = Path(__file__).resolve().parent.parent
settings = json.loads((root / "config/CS2Turrets.json").read_text(encoding="utf-8"))
config_source = (root / "CS2Turrets/TurretConfig.cs").read_text(encoding="utf-8")
plugin_source = (root / "CS2Turrets/TurretPlugin.cs").read_text(encoding="utf-8")

assert settings["MaxLevel"] == settings["RocketLevel"] == 4
assert settings["MaxActiveRockets"] > 0
assert 0 < settings["RocketDamage"] <= 250
assert 0 < settings["RocketRadius"] <= 512
assert 100 <= settings["RocketSpeed"] <= 2500
assert settings["RocketLifeSeconds"] > 0
assert settings["RocketFireInterval"] >= .3
assert settings["RocketVisualModel"].endswith(".vmdl")
assert settings["RocketLauncherModel"].endswith(".vmdl")
assert settings["RocketExplosionParticle"].endswith(".vpcf")
assert "MaxLevel { get; set; } = 4" in config_source

for method in ("AddRocketLauncher", "LaunchRocket", "AdvanceRockets", "Detonate",
               "RemoveRocket", "SpawnExplosion"):
    assert f"private void {method}(" in plugin_source, method

print("PASS: level-4 rocket defaults and required methods verified (static smoke checks)")
