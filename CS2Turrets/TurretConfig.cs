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
    [JsonPropertyName("MaxLevel")] public int MaxLevel { get; set; } = 3;
    [JsonPropertyName("DamagePerLevelBonus")] public int DamagePerLevelBonus { get; set; } = 4;
    [JsonPropertyName("RangePerLevelBonus")] public float RangePerLevelBonus { get; set; } = 100;
    [JsonPropertyName("UpgradeCooldownSeconds")] public float UpgradeCooldownSeconds { get; set; } = 2;
    // Must be a model available on your CS2 server. This can be changed per server.
    // Composite visual assembled from built-in CS2 assets; no custom model package required.
    [JsonPropertyName("UseCompositeVisual")] public bool UseCompositeVisual { get; set; } = true;
    [JsonPropertyName("BaseModel")] public string BaseModel { get; set; } = "models/props/crates/csgo_drop_crate_dangerzone.vmdl";
    [JsonPropertyName("GunModel")] public string GunModel { get; set; } = "weapons/models/m249/weapon_mach_m249.vmdl";
    [JsonPropertyName("BaseOffsetZ")] public float BaseOffsetZ { get; set; } = -2f;
    [JsonPropertyName("GunOffsetForward")] public float GunOffsetForward { get; set; } = 4f;
    [JsonPropertyName("GunOffsetZ")] public float GunOffsetZ { get; set; } = 20f;
    [JsonPropertyName("GunPitch")] public float GunPitch { get; set; } = -10f;
    [JsonPropertyName("Model")] public string Model { get; set; } = "";
    [JsonPropertyName("Rapid")] public TurretStats Rapid { get; set; } = new() { Damage = 9, Range = 750, FireInterval = 0.38f };
    [JsonPropertyName("Heavy")] public TurretStats Heavy { get; set; } = new() { Damage = 22, Range = 600, FireInterval = 1.05f };
}

public sealed class TurretStats
{
    [JsonPropertyName("Damage")] public int Damage { get; set; } = 10;
    [JsonPropertyName("Range")] public float Range { get; set; } = 700;
    [JsonPropertyName("FireInterval")] public float FireInterval { get; set; } = 0.5f;
}
