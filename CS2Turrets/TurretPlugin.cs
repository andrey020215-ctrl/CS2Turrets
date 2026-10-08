using System;
using System.Collections.Generic;
using System.Linq;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;

namespace CS2Turrets;

public sealed class TurretPlugin : BasePlugin, IPluginConfig<TurretConfig>
{
    public override string ModuleName => "CS2 Turrets";
    public override string ModuleVersion => "0.1.0-preview";
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
        public CBaseModelEntity? Prop;
    }

    private readonly List<Turret> _turrets = new();
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
        var destination = new Vector(pos.X + (float)(Math.Cos(yaw) * Config.MaxPlacementDistance),
            pos.Y + (float)(Math.Sin(yaw) * Config.MaxPlacementDistance), pos.Z + 64f);
        var eye = new Vector(pos.X, pos.Y, pos.Z + 64f);
        var trace = Trace.TraceRay(eye, destination, pawn);
        if (!trace.DidHit)
        { p.PrintToChat("[Turrets] Aim at a nearby solid surface."); return; }
        var hit = trace.EndPos;
        if (_turrets.Any(t => Distance(t.Position, hit) < Config.MinimumTurretSpacing))
        { p.PrintToChat("[Turrets] Too close to another turret."); return; }

        // The model is optional so a missing model never blocks turret gameplay.
        CBaseModelEntity? prop = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(Config.Model))
            {
                prop = Utilities.CreateEntityByName<CBaseModelEntity>("prop_dynamic");
                if (prop != null && prop.IsValid)
                {
                    prop.SetModel(Config.Model);
                    prop.Teleport(hit, new QAngle(0, a.Y, 0), null);
                    prop.DispatchSpawn();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Turret prop could not be created; using logical turret only");
            prop = null;
        }
        _turrets.Add(new Turret { Owner = p.SteamID, Team = p.Team, Kind = kind,
            Position = new Vector(hit.X, hit.Y, hit.Z), Prop = prop });
        _placing.Remove(p.SteamID);
        p.PrintToChat("[Turrets] " + kind + " placed! Press E nearby to upgrade.");
    }

    private void OnTick()
    {
        if (!Config.Enabled) return;
        _clock += 1f / 64f;
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
            turret.NextShot = _clock + Math.Max(0.15f, stats.FireInterval);
            Damage(target, Math.Max(1, stats.Damage + (turret.Level - 1) * Config.DamagePerLevelBonus));
        }
    }

    private static bool Visible(Turret turret, CCSPlayerController target)
    {
        var pawn = target.PlayerPawn.Value;
        if (pawn?.AbsOrigin == null) return false;
        var start = new Vector(turret.Position.X, turret.Position.Y, turret.Position.Z + 30);
        var end = new Vector(pawn.AbsOrigin.X, pawn.AbsOrigin.Y, pawn.AbsOrigin.Z + 36);
        var ray = Trace.TraceRay(start, end);
        return !ray.DidHit || Distance(ray.EndPos, end) < 52;
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
        _placing.Remove(steam); _wasUsing.Remove(steam); _nextUpgrade.Remove(steam);
    }

    private void Remove(Turret turret)
    {
        try { if (turret.Prop != null && turret.Prop.IsValid) turret.Prop.Remove(); }
        catch (Exception ex) { Logger.LogWarning(ex, "Unable to remove turret prop"); }
        _turrets.Remove(turret);
    }

    private void ClearAll()
    {
        foreach (var t in _turrets.ToArray()) Remove(t);
        _placing.Clear(); _wasUsing.Clear(); _nextUpgrade.Clear();
    }
    private void OnMapStart(string map) { ClearAll(); _clock = 0; _nextThink = 0; }
    public override void Unload(bool hotReload) => ClearAll();
}
