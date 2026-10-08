using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Timers;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace WinterSnow;

public sealed class WinterSnowConfig : BasePluginConfig
{
    [JsonPropertyName("Enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("AdminPermission")] public string AdminPermission { get; set; } = "@css/root";
    [JsonPropertyName("ParticleEffect")] public string ParticleEffect { get; set; } = "particles/rain_fx/snow.vpcf";
    [JsonPropertyName("UpdateSeconds")] public float UpdateSeconds { get; set; } = 0.25f;
    [JsonPropertyName("HeightOffset")] public float HeightOffset { get; set; } = 96f;
    [JsonPropertyName("MaxPlayersWithSnow")] public int MaxPlayersWithSnow { get; set; } = 16;
}

[MinimumApiVersion(376)]
public sealed class WinterSnowPlugin : BasePlugin, IPluginConfig<WinterSnowConfig>
{
    public override string ModuleName => "WinterSnow Source2";
    public override string ModuleVersion => "2.0.0";
    public override string ModuleAuthor => "OpenAI / Andrey";
    public override string ModuleDescription => "Real Source 2 snowfall using CS2 built-in particles/rain_fx/snow.vpcf.";

    public WinterSnowConfig Config { get; set; } = new();
    private readonly Dictionary<int, CParticleSystem> _emitters = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _timer;
    private bool _enabled;

    public void OnConfigParsed(WinterSnowConfig config)
    {
        config.UpdateSeconds = Math.Clamp(config.UpdateSeconds, 0.10f, 1.0f);
        config.HeightOffset = Math.Clamp(config.HeightOffset, 0f, 512f);
        config.MaxPlayersWithSnow = Math.Clamp(config.MaxPlayersWithSnow, 1, 32);
        if (string.IsNullOrWhiteSpace(config.ParticleEffect))
            config.ParticleEffect = "particles/rain_fx/snow.vpcf";
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        _enabled = Config.Enabled;
        RegisterListener<Listeners.OnMapStart>(_ => Restart());
        Restart();
        Logger.LogInformation("WinterSnow Source2 v2.0.0 loaded. Effect: {Effect}", Config.ParticleEffect);
    }

    public override void Unload(bool hotReload)
    {
        _timer?.Kill();
        Clear();
    }

    [ConsoleCommand("css_snow", "WinterSnow: on/off/toggle/status/restart")]
    public void CmdSnow(CCSPlayerController? player, CommandInfo info)
    {
        if (player is not null && !AdminManager.PlayerHasPermissions(player, Config.AdminPermission))
        {
            info.ReplyToCommand("[WinterSnow] Нет доступа.");
            return;
        }

        var arg = info.ArgCount > 1 ? info.GetArg(1).ToLowerInvariant() : "toggle";
        switch (arg)
        {
            case "on":
                _enabled = true;
                break;
            case "off":
                _enabled = false;
                Clear();
                break;
            case "restart":
                _enabled = true;
                Restart();
                break;
            case "status":
                info.ReplyToCommand($"[WinterSnow] enabled={_enabled}, emitters={_emitters.Count}, effect={Config.ParticleEffect}");
                return;
            default:
                _enabled = !_enabled;
                if (!_enabled) Clear();
                break;
        }

        info.ReplyToCommand($"[WinterSnow] Снег {(_enabled ? "включён" : "выключен")}. Emitters: {_emitters.Count}");
    }

    [ConsoleCommand("css_winterstatus", "WinterSnow diagnostic status")]
    public void CmdStatus(CCSPlayerController? player, CommandInfo info)
    {
        info.ReplyToCommand($"[WinterSnow] v{ModuleVersion}; enabled={_enabled}; emitters={_emitters.Count}; effect={Config.ParticleEffect}");
    }

    private void Restart()
    {
        _timer?.Kill();
        Clear();
        _timer = AddTimer(Config.UpdateSeconds, Tick, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
        AddTimer(1.0f, Tick, TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void Tick()
    {
        if (!_enabled) return;

        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && p.PawnIsAlive && p.PlayerPawn.Value?.AbsOrigin is not null)
            .Take(Config.MaxPlayersWithSnow)
            .ToList();

        var activeSlots = new HashSet<int>(players.Select(p => p.Slot));

        foreach (var slot in _emitters.Keys.Where(s => !activeSlots.Contains(s)).ToList())
            RemoveEmitter(slot);

        foreach (var player in players)
        {
            var origin = player.PlayerPawn.Value?.AbsOrigin;
            if (origin is null) continue;

            if (!_emitters.TryGetValue(player.Slot, out var emitter) || !emitter.IsValid)
            {
                emitter = CreateEmitter(origin);
                if (emitter is null) continue;
                _emitters[player.Slot] = emitter;
            }

            emitter.Teleport(new Vector(origin.X, origin.Y, origin.Z + Config.HeightOffset), null, null);
        }
    }

    private CParticleSystem? CreateEmitter(Vector origin)
    {
        var p = Utilities.CreateEntityByName<CParticleSystem>("info_particle_system");
        if (p is null || !p.IsValid)
        {
            Logger.LogError("WinterSnow: failed to create info_particle_system.");
            return null;
        }

        p.EffectName = Config.ParticleEffect;
        p.StartActive = true;
        p.Active = true;
        p.Teleport(new Vector(origin.X, origin.Y, origin.Z + Config.HeightOffset), null, null);
        p.DispatchSpawn();

        // Explicit start is useful on late-created particle systems.
        p.AcceptInput("Start", p, p, "", 0);

        Logger.LogInformation("WinterSnow emitter created: entity={EntityIndex}, effect={Effect}", p.Index, Config.ParticleEffect);
        return p;
    }

    private void RemoveEmitter(int slot)
    {
        if (_emitters.TryGetValue(slot, out var p) && p.IsValid)
            p.Remove();
        _emitters.Remove(slot);
    }

    private void Clear()
    {
        foreach (var p in _emitters.Values)
            if (p.IsValid) p.Remove();
        _emitters.Clear();
    }
}
