using System.Text.Json.Serialization;
using CounterStrikeSharp.API.Core;

namespace CS2Turrets;

public sealed class TurretConfig : BasePluginConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("InterceptDropKey")] public bool InterceptDropKey { get; set; } = true;
    [JsonPropertyName("AllowBotsAsTargets")] public bool AllowBotsAsTargets { get; set; } = true;
    [JsonPropertyName("MaxTurretsPerPlayer")] public int MaxTurretsPerPlayer { get; set; } = 2;
    [JsonPropertyName("MaxTurretsPerTeam")] public int MaxTurretsPerTeam { get; set; } = 12;
    [JsonPropertyName("MaxPlacementDistance")] public float MaxPlacementDistance { get; set; } = 320;
    [JsonPropertyName("MinimumTurretSpacing")] public float MinimumTurretSpacing { get; set; } = 95;
    [JsonPropertyName("UpgradeDistance")] public float UpgradeDistance { get; set; } = 120;
    [JsonPropertyName("TargetCheckInterval")] public float TargetCheckInterval { get; set; } = 0.15f;
    [JsonPropertyName("MaxLevel")] public int MaxLevel { get; set; } = 4;
    [JsonPropertyName("DamagePerLevelBonus")] public int DamagePerLevelBonus { get; set; } = 4;
    [JsonPropertyName("RangePerLevelBonus")] public float RangePerLevelBonus { get; set; } = 100;
    [JsonPropertyName("UpgradeCooldownSeconds")] public float UpgradeCooldownSeconds { get; set; } = 2;
    // Must be a model available on your CS2 server. This can be changed per server.
    // Composite visual assembled from built-in CS2 assets; no custom model package required.
    [JsonPropertyName("UseCompositeVisual")] public bool UseCompositeVisual { get; set; } = true;
    // Enable only AFTER Source 2 ModelDoc has compiled and distributed the assets.
    // GLB/FBX files in the model source bundle cannot be loaded directly by CS2.
    [JsonPropertyName("UseCustomTurretModel")] public bool UseCustomTurretModel { get; set; } = false;
    [JsonPropertyName("CustomStandardModel")] public string CustomStandardModel { get; set; } = "models/cs2turrets/turret_lv1.vmdl";
    [JsonPropertyName("CustomRocketModel")] public string CustomRocketModel { get; set; } = "models/cs2turrets/turret_lv4.vmdl";
    [JsonPropertyName("CustomProjectileModel")] public string CustomProjectileModel { get; set; } = "models/cs2turrets/rocket.vmdl";

    [JsonPropertyName("BaseModel")] public string BaseModel { get; set; } = "models/props/crates/csgo_drop_crate_dangerzone.vmdl";
    [JsonPropertyName("GunModel")] public string GunModel { get; set; } = "weapons/models/m249/weapon_mach_m249.vmdl";
    [JsonPropertyName("BaseOffsetZ")] public float BaseOffsetZ { get; set; } = -2f;
    [JsonPropertyName("GunOffsetForward")] public float GunOffsetForward { get; set; } = 4f;
    [JsonPropertyName("GunOffsetZ")] public float GunOffsetZ { get; set; } = 20f;
    [JsonPropertyName("GunPitch")] public float GunPitch { get; set; } = -10f;
    [JsonPropertyName("Model")] public string Model { get; set; } = "";
    // Level 4 rocket launcher. The stock HE grenade is only a visible placeholder
    // for the warhead, NOT a compiled custom rocket model.
    [JsonPropertyName("RocketLevel")] public int RocketLevel { get; set; } = 4;
    [JsonPropertyName("RocketLauncherModel")] public string RocketLauncherModel { get; set; } = "weapons/models/negev/weapon_mach_negev.vmdl";
    [JsonPropertyName("RocketVisualModel")] public string RocketVisualModel { get; set; } = "weapons/models/grenade/hegrenade/weapon_hegrenade.vmdl";
    [JsonPropertyName("RocketLauncherHeight")] public float RocketLauncherHeight { get; set; } = 50f;
    [JsonPropertyName("LoadedRocketHeight")] public float LoadedRocketHeight { get; set; } = 66f;
    [JsonPropertyName("LoadedRocketForward")] public float LoadedRocketForward { get; set; } = 16f;
    [JsonPropertyName("RocketSpawnHeight")] public float RocketSpawnHeight { get; set; } = 75f;
    [JsonPropertyName("RocketSpawnForward")] public float RocketSpawnForward { get; set; } = 62f;
    [JsonPropertyName("RocketDamage")] public int RocketDamage { get; set; } = 90;
    [JsonPropertyName("RocketRadius")] public float RocketRadius { get; set; } = 145f;
    [JsonPropertyName("RocketSpeed")] public float RocketSpeed { get; set; } = 800f;
    [JsonPropertyName("RocketFireInterval")] public float RocketFireInterval { get; set; } = 2.5f;
    [JsonPropertyName("RocketLifeSeconds")] public float RocketLifeSeconds { get; set; } = 3f;
    [JsonPropertyName("RocketHitRadius")] public float RocketHitRadius { get; set; } = 36f;
    [JsonPropertyName("MaxActiveRockets")] public int MaxActiveRockets { get; set; } = 24;
    [JsonPropertyName("RocketFriendlyFire")] public bool RocketFriendlyFire { get; set; } = false;
    [JsonPropertyName("EnableRocketExplosionEffects")] public bool EnableRocketExplosionEffects { get; set; } = true;
    [JsonPropertyName("RocketExplosionParticle")] public string RocketExplosionParticle { get; set; } = "particles/explosions_fx/explosion_c4_short.vpcf";
    [JsonPropertyName("ExplosionEffectLifetime")] public float ExplosionEffectLifetime { get; set; } = 0.7f;
    [JsonPropertyName("Rapid")] public TurretStats Rapid { get; set; } = new() { Damage = 9, Range = 750, FireInterval = 0.38f };
    [JsonPropertyName("Heavy")] public TurretStats Heavy { get; set; } = new() { Damage = 22, Range = 600, FireInterval = 1.05f };
}

public sealed class TurretStats
{
    [JsonPropertyName("Damage")] public int Damage { get; set; } = 10;
    [JsonPropertyName("Range")] public float Range { get; set; } = 700;
    [JsonPropertyName("FireInterval")] public float FireInterval { get; set; } = 0.5f;
}
