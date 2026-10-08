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
    [JsonPropertyName("FlakesPerPlayer")] public int FlakesPerPlayer { get; set; } = 18;
    [JsonPropertyName("MaxPlayersWithSnow")] public int MaxPlayersWithSnow { get; set; } = 8;
    [JsonPropertyName("SpawnRadius")] public float SpawnRadius { get; set; } = 700f;
    [JsonPropertyName("SpawnHeightMin")] public float SpawnHeightMin { get; set; } = 260f;
    [JsonPropertyName("SpawnHeightMax")] public float SpawnHeightMax { get; set; } = 500f;
    [JsonPropertyName("FallSpeedMin")] public float FallSpeedMin { get; set; } = 85f;
    [JsonPropertyName("FallSpeedMax")] public float FallSpeedMax { get; set; } = 145f;
    [JsonPropertyName("HorizontalDrift")] public float HorizontalDrift { get; set; } = 24f;
    [JsonPropertyName("BounceSpeed")] public float BounceSpeed { get; set; } = 68f;
    [JsonPropertyName("BounceCount")] public int BounceCount { get; set; } = 1;
    [JsonPropertyName("FadeSeconds")] public float FadeSeconds { get; set; } = 0.42f;
    [JsonPropertyName("TickSeconds")] public float TickSeconds { get; set; } = 0.05f;
    [JsonPropertyName("Glyph")] public string Glyph { get; set; } = "❄";
    [JsonPropertyName("FontSizeMin")] public float FontSizeMin { get; set; } = 22f;
    [JsonPropertyName("FontSizeMax")] public float FontSizeMax { get; set; } = 38f;
}

[MinimumApiVersion(376)]
public sealed class WinterSnowPlugin : BasePlugin, IPluginConfig<WinterSnowConfig>
{
    public override string ModuleName => "WinterSnow Ready";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "OpenAI / Andrey";
    public override string ModuleDescription => "Glowing animated snowflakes with ground bounce and fade, no custom client asset required.";

    public WinterSnowConfig Config { get; set; } = new();
    private readonly List<Flake> _flakes = new();
    private CounterStrikeSharp.API.Modules.Timers.Timer? _timer;
    private readonly Random _rng = new();
    private bool _enabled;

    private sealed class Flake
    {
        public required CPointWorldText Entity { get; init; }
        public required Vector Pos { get; set; }
        public required Vector Vel { get; set; }
        public required float BaseSize { get; init; }
        public int Bounces { get; set; }
        public float FadeLeft { get; set; } = -1f;
    }

    public void OnConfigParsed(WinterSnowConfig config)
    {
        config.FlakesPerPlayer = Math.Clamp(config.FlakesPerPlayer, 4, 40);
        config.MaxPlayersWithSnow = Math.Clamp(config.MaxPlayersWithSnow, 1, 20);
        config.TickSeconds = Math.Clamp(config.TickSeconds, 0.03f, 0.15f);
        config.BounceCount = Math.Clamp(config.BounceCount, 0, 2);
        config.FadeSeconds = Math.Clamp(config.FadeSeconds, 0.15f, 1.5f);
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        _enabled = Config.Enabled;
        RegisterListener<Listeners.OnMapStart>(_ => Restart());
        Restart();
        Logger.LogInformation("WinterSnow Ready loaded. No Workshop asset required.");
    }

    public override void Unload(bool hotReload)
    {
        _timer?.Kill();
        Clear();
    }

    [ConsoleCommand("css_snow", "!snow on/off/toggle")]
    public void CmdSnow(CCSPlayerController? player, CommandInfo info)
    {
        if (player is not null && !AdminManager.PlayerHasPermissions(player, Config.AdminPermission))
        {
            info.ReplyToCommand("[WinterSnow] Нет доступа.");
            return;
        }
        var arg = info.ArgCount > 1 ? info.GetArg(1).ToLowerInvariant() : "toggle";
        _enabled = arg switch { "on" => true, "off" => false, _ => !_enabled };
        if (!_enabled) Clear();
        info.ReplyToCommand($"[WinterSnow] Снег {(_enabled ? "включён" : "выключен")}");
    }

    private void Restart()
    {
        _timer?.Kill();
        Clear();
        _timer = AddTimer(Config.TickSeconds, Tick, TimerFlags.REPEAT | TimerFlags.STOP_ON_MAPCHANGE);
    }

    private void Tick()
    {
        if (!_enabled) return;
        var players = Utilities.GetPlayers()
            .Where(p => p.IsValid && !p.IsBot && p.PawnIsAlive && p.PlayerPawn.Value?.AbsOrigin is not null)
            .Take(Config.MaxPlayersWithSnow).ToList();

        int target = players.Count * Config.FlakesPerPlayer;
        while (_flakes.Count < target && players.Count > 0)
            Spawn(players[_flakes.Count % players.Count]);
        while (_flakes.Count > target)
            RemoveAt(_flakes.Count - 1);

        float dt = Config.TickSeconds;
        for (int i = _flakes.Count - 1; i >= 0; i--)
        {
            var f = _flakes[i];
            if (!f.Entity.IsValid) { _flakes.RemoveAt(i); continue; }

            if (f.FadeLeft >= 0f)
            {
                f.FadeLeft -= dt;
                float k = Math.Clamp(f.FadeLeft / Config.FadeSeconds, 0f, 1f);
                f.Entity.Color = new Color(205, 235, 255, (byte)(235 * k));
                f.Entity.FontSize = Math.Max(4f, f.BaseSize * (0.65f + 0.35f * k));
                Utilities.SetStateChanged(f.Entity, "CPointWorldText", "m_Color");
                Utilities.SetStateChanged(f.Entity, "CPointWorldText", "m_flFontSize");
                if (f.FadeLeft <= 0f)
                {
                    Respawn(f, players);
                    continue;
                }
            }

            var next = new Vector(f.Pos.X + f.Vel.X * dt, f.Pos.Y + f.Vel.Y * dt, f.Pos.Z + f.Vel.Z * dt);
            var tr = Trace.TraceEndShape(f.Pos, next);
            if (tr.Fraction < 0.999f)
            {
                f.Pos = new Vector(tr.EndPos.X, tr.EndPos.Y, tr.EndPos.Z + 1.5f);
                if (f.Bounces < Config.BounceCount)
                {
                    f.Bounces++;
                    f.Vel = new Vector(f.Vel.X * 0.45f, f.Vel.Y * 0.45f, Config.BounceSpeed * (0.8f + (float)_rng.NextDouble() * 0.35f));
                }
                else
                {
                    f.Vel = new Vector(0, 0, 0);
                    f.FadeLeft = Config.FadeSeconds;
                }
            }
            else
            {
                f.Pos = next;
                f.Vel = new Vector(f.Vel.X, f.Vel.Y, f.Vel.Z - 28f * dt);
            }

            f.Entity.Teleport(f.Pos, null, null);
        }
    }

    private void Spawn(CCSPlayerController player)
    {
        var origin = player.PlayerPawn.Value!.AbsOrigin!;
        var e = Utilities.CreateEntityByName<CPointWorldText>("point_worldtext");
        if (e is null || !e.IsValid) return;

        float a = (float)(_rng.NextDouble() * Math.PI * 2.0);
        float r = (float)Math.Sqrt(_rng.NextDouble()) * Config.SpawnRadius;
        float z = Config.SpawnHeightMin + (float)_rng.NextDouble() * (Config.SpawnHeightMax - Config.SpawnHeightMin);
        var pos = new Vector(origin.X + MathF.Cos(a) * r, origin.Y + MathF.Sin(a) * r, origin.Z + z);
        float size = Config.FontSizeMin + (float)_rng.NextDouble() * (Config.FontSizeMax - Config.FontSizeMin);
        float vx = ((float)_rng.NextDouble() * 2f - 1f) * Config.HorizontalDrift;
        float vy = ((float)_rng.NextDouble() * 2f - 1f) * Config.HorizontalDrift;
        float vz = -(Config.FallSpeedMin + (float)_rng.NextDouble() * (Config.FallSpeedMax - Config.FallSpeedMin));

        e.MessageText = Config.Glyph;
        e.Color = new Color(205, 235, 255, 235);
        e.FontSize = size;
        e.WorldUnitsPerPx = 0.08f;
        e.Fullbright = true;
        e.Enabled = true;
        e.DrawBackground = false;
        e.Teleport(pos, new QAngle(90, 0, 0), null);
        e.DispatchSpawn();

        _flakes.Add(new Flake { Entity = e, Pos = pos, Vel = new Vector(vx, vy, vz), BaseSize = size });
    }

    private void Respawn(Flake f, List<CCSPlayerController> players)
    {
        if (players.Count == 0) { f.FadeLeft = 0f; return; }
        var p = players[_rng.Next(players.Count)];
        var o = p.PlayerPawn.Value?.AbsOrigin;
        if (o is null) return;
        float a = (float)(_rng.NextDouble() * Math.PI * 2.0);
        float r = (float)Math.Sqrt(_rng.NextDouble()) * Config.SpawnRadius;
        f.Pos = new Vector(o.X + MathF.Cos(a) * r, o.Y + MathF.Sin(a) * r, o.Z + Config.SpawnHeightMin + (float)_rng.NextDouble() * (Config.SpawnHeightMax - Config.SpawnHeightMin));
        f.Vel = new Vector(((float)_rng.NextDouble() * 2f - 1f) * Config.HorizontalDrift, ((float)_rng.NextDouble() * 2f - 1f) * Config.HorizontalDrift, -(Config.FallSpeedMin + (float)_rng.NextDouble() * (Config.FallSpeedMax - Config.FallSpeedMin)));
        f.Bounces = 0;
        f.FadeLeft = -1f;
        f.Entity.Color = new Color(205, 235, 255, 235);
        f.Entity.FontSize = f.BaseSize;
        Utilities.SetStateChanged(f.Entity, "CPointWorldText", "m_Color");
        Utilities.SetStateChanged(f.Entity, "CPointWorldText", "m_flFontSize");
        f.Entity.Teleport(f.Pos, null, null);
    }

    private void RemoveAt(int i)
    {
        if (_flakes[i].Entity.IsValid) _flakes[i].Entity.Remove();
        _flakes.RemoveAt(i);
    }

    private void Clear()
    {
        foreach (var f in _flakes) if (f.Entity.IsValid) f.Entity.Remove();
        _flakes.Clear();
    }
}
