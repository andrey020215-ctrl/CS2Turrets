using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using Microsoft.Extensions.Logging;

namespace CS2Turrets;

public sealed class TurretPlugin : BasePlugin, IPluginConfig<TurretConfig>
{
    public override string ModuleName => "CS2 Turrets";
    public override string ModuleVersion => "0.3.0-preview";
    public override string ModuleAuthor => "CS2Turrets";
    public override string ModuleDescription => "Place, upgrade and automate team turrets";
    public TurretConfig Config { get; set; } = new();
    public void OnConfigParsed(TurretConfig config) => Config = config;

    private sealed class Turret
    {
        public required ulong Owner;
        public required CsTeam Team;
        public required string Kind;
        public required Vector Position;
        public int Level = 1;
        public float NextShot;
        public float Yaw;
        public readonly List<CBaseModelEntity> Visuals = new();
    }

    private sealed class Rocket
    {
        public required ulong Owner;
        public required CsTeam Team;
        public required Vector Position;
        public required Vector Direction;
        public float ExpiresAt;
        public CBaseModelEntity? Visual;
    }

    private readonly List<Turret> _turrets = new();
    private readonly List<Rocket> _rockets = new();
    private readonly Dictionary<ulong, string> _placing = new();
    private readonly HashSet<ulong> _wasUsing = new();
    private float _nextThink;
    private float _clock;
    private readonly Dictionary<ulong, float> _nextUpgrade = new();

    public override void Load(bool hotReload)
    {
        AddCommand("css_turret", "Turret build menu", (p, _) => { if (Usable(p)) ShowMenu(p!); });
        AddCommand("css_turret_place", "Place selected turret", (p, _) => { if (Usable(p)) Place(p!); });
        AddCommand("css_turret_cancel", "Cancel placement", (p, _) => { if (p != null) _placing.Remove(p.SteamID); });
        if (Config.InterceptDropKey) AddCommandListener("drop", OnDrop);
        RegisterListener<Listeners.OnTick>(OnTick);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterEventHandler<EventRoundStart>((_, _) => { ClearAll(); return HookResult.Continue; });
        RegisterEventHandler<EventPlayerDisconnect>((e, _) =>
        {
            if (e.Userid != null) RemoveOwner(e.Userid.SteamID);
            return HookResult.Continue;
        });
    }

    private HookResult OnDrop(CCSPlayerController? player, CommandInfo command)
    {
        if (!Usable(player) || !Config.Enabled) return HookResult.Continue;
        if (_placing.ContainsKey(player!.SteamID)) Place(player);
        else ShowMenu(player);
        return HookResult.Handled;
    }

    private static bool Usable(CCSPlayerController? p) =>
        p != null && p.IsValid && p.PawnIsAlive &&
        p.Team is CsTeam.Terrorist or CsTeam.CounterTerrorist;

    private void ShowMenu(CCSPlayerController player)
    {
        var menu = new CenterHtmlMenu("TURRET SYSTEM", this);
        menu.AddMenuOption("1. Rapid turret", (p, _) => Select(p, "Rapid"));
        menu.AddMenuOption("2. Heavy turret", (p, _) => Select(p, "Heavy"));
        menu.AddMenuOption("3. Cancel placement", (p, _) =>
        {
            _placing.Remove(p.SteamID); p.PrintToChat("[Turrets] Placement cancelled.");
        });
        MenuManager.OpenCenterHtmlMenu(this, player, menu);
    }

    private void Select(CCSPlayerController p, string kind)
    {
        if (!Usable(p)) return;
        _placing[p.SteamID] = kind;
        p.PrintToChat("[Turrets] Aim at flat ground and press G (drop) to place.");
        p.PrintToChat("[Turrets] Or type !turret_place. Press E near your turret to upgrade.");
    }

    private void Place(CCSPlayerController p)
    {
        if (!_placing.TryGetValue(p.SteamID, out var kind)) { ShowMenu(p); return; }
        if (_turrets.Count(t => t.Owner == p.SteamID) >= Math.Max(0, Config.MaxTurretsPerPlayer))
        { p.PrintToChat("[Turrets] Personal limit reached."); return; }
        if (_turrets.Count(t => t.Team == p.Team) >= Math.Max(0, Config.MaxTurretsPerTeam))
        { p.PrintToChat("[Turrets] Team limit reached."); return; }
        var pawn = p.PlayerPawn.Value;
        if (pawn == null || pawn.AbsOrigin == null || pawn.EyeAngles == null) return;
        var pos = pawn.AbsOrigin;
        var a = pawn.EyeAngles;
        var yaw = a.Y * Math.PI / 180.0;
        var pitch = a.X * Math.PI / 180.0;
        var horizontal = Math.Cos(pitch) * Config.MaxPlacementDistance;
        var destination = new Vector(pos.X + (float)(Math.Cos(yaw) * horizontal),
            pos.Y + (float)(Math.Sin(yaw) * horizontal), pos.Z + 64f - (float)(Math.Sin(pitch) * Config.MaxPlacementDistance));
        var eye = new Vector(pos.X, pos.Y, pos.Z + 64f);
        var trace = Trace.TraceEndShape(eye, destination, pawn);
        if (!trace.DidHit() || trace.Normal.Z < 0.65f)
        { p.PrintToChat("[Turrets] Aim at a nearby solid surface."); return; }
        var hit = trace.EndPos;
        if (_turrets.Any(t => Distance(t.Position, hit) < Config.MinimumTurretSpacing))
        { p.PrintToChat("[Turrets] Too close to another turret."); return; }

        var turret = new Turret
        {
            Owner = p.SteamID,
            Team = p.Team,
            Kind = kind,
            Position = new Vector(hit.X, hit.Y, hit.Z),
            Yaw = a.Y
        };
        SpawnVisuals(turret);
        _turrets.Add(turret);
        _placing.Remove(p.SteamID);
        p.PrintToChat("[Turrets] " + kind + " placed! Press E nearby to upgrade.");
    }

    // Uses two stock CS2 models. Both props are tracked for cleanup.
    private void SpawnVisuals(Turret turret)
    {
        if (Config.UseCompositeVisual)
        {
            TrySpawnVisual(turret, Config.BaseModel,
                new Vector(turret.Position.X, turret.Position.Y, turret.Position.Z + Config.BaseOffsetZ),
                new QAngle(0, turret.Yaw, 0));

            var angleRadians = turret.Yaw * MathF.PI / 180f;
            TrySpawnVisual(turret, Config.GunModel,
                new Vector(
                    turret.Position.X + MathF.Cos(angleRadians) * Config.GunOffsetForward,
                    turret.Position.Y + MathF.Sin(angleRadians) * Config.GunOffsetForward,
                    turret.Position.Z + Config.GunOffsetZ),
                new QAngle(Config.GunPitch, turret.Yaw, 0));
        }
        else
        {
            TrySpawnVisual(turret, Config.Model, turret.Position, new QAngle(0, turret.Yaw, 0));
        }
    }

    private void TrySpawnVisual(Turret turret, string model, Vector position, QAngle rotation)
    {
        if (string.IsNullOrWhiteSpace(model)) return;
        CBaseModelEntity? prop = null;
        try
        {
            prop = Utilities.CreateEntityByName<CBaseModelEntity>("prop_dynamic");
            if (prop == null || !prop.IsValid) return;
            prop.SetModel(model);
            prop.Teleport(position, rotation, null);
            prop.DispatchSpawn();
            turret.Visuals.Add(prop);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to spawn turret model {Model}", model);
            if (prop != null && prop.IsValid) prop.Remove();
        }
    }


    private static Vector ForwardOffset(Vector origin, float yaw, float forward, float up)
    {
        var a = yaw * MathF.PI / 180f;
        return new Vector(origin.X + MathF.Cos(a) * forward,
            origin.Y + MathF.Sin(a) * forward, origin.Z + up);
    }

    // A temporary stock-game grenade mesh represents the missile's warhead.
    private void AddRocketLauncher(Turret turret)
    {
        if (!Config.UseCompositeVisual) return;
        TrySpawnVisual(turret, Config.RocketLauncherModel,
            ForwardOffset(turret.Position, turret.Yaw, 0, Config.RocketLauncherHeight),
            new QAngle(0, turret.Yaw, 0));
        TrySpawnVisual(turret, Config.RocketVisualModel,
            ForwardOffset(turret.Position, turret.Yaw, Config.LoadedRocketForward, Config.LoadedRocketHeight),
            new QAngle(0, turret.Yaw, 0));
    }

    private static Vector Normalize(Vector delta)
    {
        var length = MathF.Sqrt(delta.X * delta.X + delta.Y * delta.Y + delta.Z * delta.Z);
        return length < .01f ? new Vector(1, 0, 0)
            : new Vector(delta.X / length, delta.Y / length, delta.Z / length);
    }

    private static QAngle AimRotation(Vector direction)
    {
        var yaw = MathF.Atan2(direction.Y, direction.X) * (180f / MathF.PI);
        var horizontal = MathF.Sqrt(direction.X * direction.X + direction.Y * direction.Y);
        return new QAngle(-MathF.Atan2(direction.Z, horizontal) * (180f / MathF.PI), yaw, 0);
    }

    private void LaunchRocket(Turret turret, CCSPlayerController target)
    {
        if (_rockets.Count >= Math.Clamp(Config.MaxActiveRockets, 0, 128)) return;
        var enemy = target.PlayerPawn.Value?.AbsOrigin;
        if (enemy == null) return;
        var start = ForwardOffset(turret.Position, turret.Yaw, Config.RocketSpawnForward, Config.RocketSpawnHeight);
        var targetPoint = new Vector(enemy.X, enemy.Y, enemy.Z + 36);
        var direction = Normalize(new Vector(targetPoint.X - start.X, targetPoint.Y - start.Y, targetPoint.Z - start.Z));

        var rocket = new Rocket
        {
            Owner = turret.Owner,
            Team = turret.Team,
            Position = start,
            Direction = direction,
            ExpiresAt = _clock + Math.Clamp(Config.RocketLifeSeconds, .25f, 10f)
        };

        CBaseModelEntity? prop = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(Config.RocketVisualModel))
            {
                prop = Utilities.CreateEntityByName<CBaseModelEntity>("prop_dynamic");
                if (prop != null && prop.IsValid)
                {
                    prop.SetModel(Config.RocketVisualModel);
                    prop.Teleport(start, AimRotation(direction), null);
                    prop.DispatchSpawn();
                    rocket.Visual = prop;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Unable to create projectile model");
            try { if (prop?.IsValid == true) prop.Remove(); }
            catch { }
        }
        _rockets.Add(rocket);
    }


    // Server-authoritative visible rocket flight, ray-cast collisions and splash damage.
    // Caps both active rockets and their lifetime to keep the tick cost bounded.
    private void AdvanceRockets()
    {
        if (_rockets.Count == 0) return;
        var step = Math.Clamp(Config.RocketSpeed, 100f, 2500f) *
            Math.Clamp(Server.TickInterval, .005f, .05f);

        foreach (var rocket in _rockets.ToArray())
        {
            if (_clock >= rocket.ExpiresAt)
            {
                RemoveRocket(rocket);
                continue;
            }

            var next = new Vector(
                rocket.Position.X + rocket.Direction.X * step,
                rocket.Position.Y + rocket.Direction.Y * step,
                rocket.Position.Z + rocket.Direction.Z * step);

            var hit = rocket.Visual?.IsValid == true
                ? Trace.TraceEndShape(rocket.Position, next, rocket.Visual)
                : Trace.TraceEndShape(rocket.Position, next);

            if (hit.DidHit())
            {
                Detonate(rocket, hit.EndPos);
                continue;
            }

            var impacted = false;
            foreach (var player in Utilities.GetPlayers().Where(Usable))
            {
                if (!Config.RocketFriendlyFire && player.Team == rocket.Team) continue;
                if (!Config.AllowBotsAsTargets && player.IsBot) continue;
                var origin = player.PlayerPawn.Value?.AbsOrigin;
                if (origin == null) continue;
                var center = new Vector(origin.X, origin.Y, origin.Z + 36);
                if (SegmentDistance(rocket.Position, next, center) >
                    Math.Clamp(Config.RocketHitRadius, 8f, 128f)) continue;
                Detonate(rocket, center);
                impacted = true;
                break;
            }
            if (impacted) continue;

            rocket.Position = next;
            if (rocket.Visual?.IsValid == true)
            {
                try { rocket.Visual.Teleport(next, AimRotation(rocket.Direction), null); }
                catch (Exception ex)
                {
                    Logger.LogWarning(ex, "Rocket movement failed");
                    RemoveRocket(rocket);
                }
            }
        }
    }

    private static float SegmentDistance(Vector start, Vector end, Vector point)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var dz = end.Z - start.Z;
        var squared = dx * dx + dy * dy + dz * dz;
        var t = squared < .0001f ? 0f : Math.Clamp(
            ((point.X - start.X) * dx + (point.Y - start.Y) * dy +
             (point.Z - start.Z) * dz) / squared, 0f, 1f);
        return Distance(new Vector(start.X + t * dx, start.Y + t * dy, start.Z + t * dz), point);
    }

    private void Detonate(Rocket rocket, Vector impact)
    {
        var radius = Math.Clamp(Config.RocketRadius, 16f, 512f);
        foreach (var player in Utilities.GetPlayers().Where(Usable))
        {
            if (!Config.RocketFriendlyFire && player.Team == rocket.Team) continue;
            if (!Config.AllowBotsAsTargets && player.IsBot) continue;
            var origin = player.PlayerPawn.Value?.AbsOrigin;
            if (origin == null) continue;
            var center = new Vector(origin.X, origin.Y, origin.Z + 36);
            var distance = Distance(impact, center);
            if (distance > radius) continue;
            var factor = 1f - .75f * distance / radius;
            var damage = Math.Max(1, (int)MathF.Round(Math.Clamp(Config.RocketDamage, 1, 250) * factor));
            Damage(player, damage);
        }
        RemoveRocket(rocket);
    }

    private void RemoveRocket(Rocket rocket)
    {
        try { if (rocket.Visual?.IsValid == true) rocket.Visual.Remove(); }
        catch (Exception ex) { Logger.LogWarning(ex, "Unable to remove rocket projectile"); }
        _rockets.Remove(rocket);
    }

    private void OnTick()
    {
        if (!Config.Enabled) return;
        _clock = Server.CurrentTime;
        AdvanceRockets();
        foreach (var player in Utilities.GetPlayers().Where(Usable))
        {
            bool isUsing = (player.Buttons & PlayerButtons.Use) != 0;
            if (isUsing && !_wasUsing.Contains(player.SteamID)) Upgrade(player);
            if (isUsing) _wasUsing.Add(player.SteamID);
            else _wasUsing.Remove(player.SteamID);
        }
        if (_clock < _nextThink) return;
        _nextThink = _clock + Math.Clamp(Config.TargetCheckInterval, 0.1f, 2f);
        var players = Utilities.GetPlayers().Where(Usable).ToArray();
        foreach (var turret in _turrets)
        {
            if (turret.NextShot > _clock) continue;
            var stats = turret.Kind == "Heavy" ? Config.Heavy : Config.Rapid;
            var range = stats.Range + (turret.Level - 1) * Config.RangePerLevelBonus;
            var target = players
                .Where(p => p.Team != turret.Team && (Config.AllowBotsAsTargets || !p.IsBot))
                .Where(p => p.PlayerPawn.Value?.AbsOrigin != null)
                .Where(p => Distance(turret.Position, p.PlayerPawn.Value!.AbsOrigin!) <= range)
                .OrderBy(p => Distance(turret.Position, p.PlayerPawn.Value!.AbsOrigin!))
                .FirstOrDefault(p => Visible(turret, p));
            if (target == null) continue;
            if (turret.Level >= Math.Max(2, Config.RocketLevel))
            {
                if (_rockets.Count >= Math.Clamp(Config.MaxActiveRockets, 0, 128)) continue;
                turret.NextShot = _clock + Math.Clamp(Config.RocketFireInterval, .3f, 15f);
                LaunchRocket(turret, target);
            }
            else
            {
                turret.NextShot = _clock + Math.Max(0.15f, stats.FireInterval);
                Damage(target, Math.Max(1, stats.Damage + (turret.Level - 1) * Config.DamagePerLevelBonus));
            }
        }
    }

    private static bool Visible(Turret turret, CCSPlayerController target)
    {
        var pawn = target.PlayerPawn.Value;
        if (pawn?.AbsOrigin == null) return false;
        var start = new Vector(turret.Position.X, turret.Position.Y, turret.Position.Z + 30);
        var end = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z + 36);
        var ray = Trace.TraceEndShape(start, end);
        return !ray.DidHit() || Distance(ray.EndPos, end) < 52;
    }

    private static void Damage(CCSPlayerController p, int damage)
    {
        var pawn = p.PlayerPawn.Value;
        if (pawn == null || !pawn.IsValid || pawn.Health <= 0) return;
        // Preview damage path. Real damage attribution should use TakeDamage when verified.
        if (pawn.Health <= damage) pawn.CommitSuicide(false, true);
        else
        {
            pawn.Health -= damage;
            Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        }
    }

    private void Upgrade(CCSPlayerController p)
    {
        if (_nextUpgrade.TryGetValue(p.SteamID, out var until) && until > _clock) return;
        var pos = p.PlayerPawn.Value?.AbsOrigin;
        if (pos == null) return;
        var turret = _turrets.Where(t => t.Owner == p.SteamID && t.Level < Config.MaxLevel)
            .Where(t => Distance(t.Position, pos) <= Config.UpgradeDistance)
            .OrderBy(t => Distance(t.Position, pos)).FirstOrDefault();
        if (turret == null) return;
        turret.Level++;
        if (turret.Level == Config.RocketLevel)
        {
            AddRocketLauncher(turret);
            turret.NextShot = _clock + 1f;
            p.PrintToChat("[Turrets] ROCKET LAUNCHER activated! Level 4 fires splash rockets.");
        }
        _nextUpgrade[p.SteamID] = _clock + Math.Max(.5f, Config.UpgradeCooldownSeconds);
        p.PrintToChat($"[Turrets] Upgraded {turret.Kind} to level {turret.Level}/{Config.MaxLevel}.");
    }

    private static float Distance(Vector a, Vector b)
    {
        var dx = a.X - b.X; var dy = a.Y - b.Y; var dz = a.Z - b.Z;
        return MathF.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    private void RemoveOwner(ulong steam)
    {
        foreach (var turret in _turrets.Where(t => t.Owner == steam).ToList()) Remove(turret);
        foreach (var rocket in _rockets.Where(r => r.Owner == steam).ToArray()) RemoveRocket(rocket);
        _placing.Remove(steam); _wasUsing.Remove(steam); _nextUpgrade.Remove(steam);
    }

    private void Remove(Turret turret)
    {
        foreach (var prop in turret.Visuals)
        {
            try { if (prop.IsValid) prop.Remove(); }
            catch (Exception ex) { Logger.LogWarning(ex, "Unable to remove turret visual"); }
        }
        turret.Visuals.Clear();
        _turrets.Remove(turret);
    }

    private void ClearAll()
    {
        foreach (var rocket in _rockets.ToArray()) RemoveRocket(rocket);
        foreach (var t in _turrets.ToArray()) Remove(t);
        _placing.Clear(); _wasUsing.Clear(); _nextUpgrade.Clear();
    }
    private void OnMapStart(string map) { ClearAll(); _clock = 0; _nextThink = 0; }
    public override void Unload(bool hotReload) => ClearAll();
}
