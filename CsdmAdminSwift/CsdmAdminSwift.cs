using Microsoft.Extensions.Logging;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Events;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Natives;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;
using SwiftlyS2.Shared.SchemaDefinitions;

namespace CsdmAdminSwift;

[PluginMetadata(
    Id = "andrey.csdmadminswift",
    Version = "2.1.0",
    Name = "CSDM Admin Menu + Winter Maps",
    Author = "OpenAI / Andrey",
    Description = "SwiftlyS2 admin menu, winter Dust2/Mirage snowfall, bonuses and turret integration")]
public sealed class CsdmAdminSwift : BasePlugin
{
    private const string SnowEffect = "particles/rain_fx/snow.vpcf";
    private readonly Dictionary<int, CParticleSystem> _emitters = new();
    private CancellationTokenSource? _snowTimer;
    private bool _snowEnabled = true;

    public CsdmAdminSwift(ISwiftlyCore core) : base(core) { }

    public override void Load(bool hotReload)
    {
        Core.Event.OnPrecacheResource += OnPrecacheResource;
        _snowTimer = Core.Scheduler.DelayAndRepeatBySeconds(2.0f, 0.50f, UpdateSnow);
        Core.Logger.LogInformation("CSDM Admin Swift loaded. Chat: !admin. Winter maps use stock Dust2/Mirage plus built-in Source2 snowfall.");
    }

    public override void Unload()
    {
        if (_snowTimer is not null)
        {
            _snowTimer.Cancel();
            _snowTimer.Dispose();
            _snowTimer = null;
        }
        Core.Event.OnPrecacheResource -= OnPrecacheResource;
        ClearSnow();
    }

    private void OnPrecacheResource(IOnPrecacheResourceEvent ev)
    {
        ev.AddItem(SnowEffect);
    }

    [Command("admin")]
    [CommandAlias("adm", true)]
    public void AdminCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply("Use !admin from a connected player.");
            return;
        }
        ShowAdmin(context.Sender);
    }

    [Command("winter")]
    public void WinterCommand(ICommandContext context)
    {
        if (context.Sender is null) return;
        ShowWinterMaps(context.Sender);
    }

    [Command("winterstatus")]
    public void WinterStatusCommand(ICommandContext context)
    {
        var map = CurrentMap();
        context.Reply($"[CSDM] map={map}; snow={_snowEnabled}; emitters={_emitters.Count}; turret=sw_turret");
    }

    private void ShowAdmin(IPlayer player)
    {
        var builder = Core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle("CSDM | ADMIN MENU");

        AddAction(builder, "WINTER MAPS", player, () => ShowWinterMaps(player), close:false);
        AddAction(builder, "Turret menu", player, () => player.ExecuteCommand("sw_turret"));
        AddAction(builder, "Money 16000", player, () => GiveMoney(player));
        AddAction(builder, "Speed boost", player, () => GiveSpeed(player));
        AddAction(builder, "Low gravity", player, () => GiveGravity(player));
        AddAction(builder, "Gold AK-47", player, () => GiveGoldWeapon(player, "weapon_ak47", 921));
        AddAction(builder, "Gold M4A1-S", player, () => GiveGoldWeapon(player, "weapon_m4a1_silencer", 497));
        AddAction(builder, "Gold Deagle", player, () => GiveGoldWeapon(player, "weapon_deagle", 185));
        AddAction(builder, "Snow ON/OFF", player, ToggleSnow);
        AddAction(builder, "Apply 60 minute round", player, Apply60MinuteRound);
        AddAction(builder, "Restart round", player, () => Core.Engine.ExecuteCommand("mp_restartgame 1"));

        Core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private void ShowWinterMaps(IPlayer player)
    {
        var builder = Core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle("CSDM | WINTER MAPS");

        AddAction(builder, "Winter Dust2", player, () =>
        {
            _snowEnabled = true;
            ClearSnow();
            Core.Engine.ExecuteCommand("changelevel de_dust2");
        });
        AddAction(builder, "Winter Mirage", player, () =>
        {
            _snowEnabled = true;
            ClearSnow();
            Core.Engine.ExecuteCommand("changelevel de_mirage");
        });
        AddAction(builder, "Normal Nuke", player, () =>
        {
            ClearSnow();
            Core.Engine.ExecuteCommand("changelevel de_nuke");
        });
        AddAction(builder, "Back", player, () => ShowAdmin(player), close:false);

        Core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private void AddAction(IMenuBuilderAPI builder, string text, IPlayer player, Action action, bool close = true)
    {
        var option = new ButtonMenuOption(text);
        option.Click += (sender, args) =>
        {
            Core.Scheduler.NextTick(() =>
            {
                if (close)
                {
                    var current = Core.MenusAPI.GetCurrentMenu(args.Player);
                    if (current is not null)
                        Core.MenusAPI.CloseMenuForPlayer(args.Player, current);
                }
                action();
            });
            return ValueTask.CompletedTask;
        };
        builder.AddOption(option);
    }

    private void GiveMoney(IPlayer player)
    {
        var money = player.Controller.InGameMoneyServices;
        if (money is null)
        {
            player.SendChat("[CSDM] Money service unavailable.");
            return;
        }
        money.Account = 16000;
        money.AccountUpdated();
        player.SendChat("[CSDM] Money: $16000.");
    }

    private void GiveSpeed(IPlayer player)
    {
        var pawn = player.PlayerPawn;
        if (pawn?.MovementServices is null)
        {
            player.SendChat("[CSDM] Spawn first.");
            return;
        }
        pawn.MovementServices.Maxspeed = 400f;
        player.SendChat("[CSDM] Speed boost enabled for this life.");
    }

    private void GiveGravity(IPlayer player)
    {
        var pawn = player.PlayerPawn;
        if (pawn is null)
        {
            player.SendChat("[CSDM] Spawn first.");
            return;
        }
        pawn.GravityScale = 0.5f;
        player.SendChat("[CSDM] Low gravity enabled for this life.");
    }

    private void GiveGoldWeapon(IPlayer player, string designerName, int paintKit)
    {
        try
        {
            var items = player.Controller.ItemServices;
            if (items is null)
            {
                player.SendChat("[CSDM] Item service unavailable.");
                return;
            }
            var weapon = items.GiveItem<CBasePlayerWeapon>(designerName);
            weapon.FallbackPaintKit = paintKit;
            weapon.FallbackSeed = 0;
            weapon.FallbackWear = 0.01f;
            weapon.FallbackPaintKitUpdated();
            weapon.FallbackSeedUpdated();
            weapon.FallbackWearUpdated();
            player.SendChat("[CSDM] Gold weapon granted.");
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "Gold weapon give failed.");
            player.SendChat("[CSDM] Could not give weapon.");
        }
    }

    private void ToggleSnow()
    {
        _snowEnabled = !_snowEnabled;
        if (!_snowEnabled) ClearSnow();
    }

    private void Apply60MinuteRound()
    {
        string[] commands =
        [
            "mp_do_warmup_period 0",
            "mp_warmuptime 0",
            "mp_warmup_end",
            "mp_ignore_round_win_conditions 0",
            "mp_roundtime 60",
            "mp_roundtime_defuse 60",
            "mp_roundtime_hostage 60",
            "mp_timelimit 0",
            "mp_maxrounds 30",
            "mp_match_can_clinch 0",
            "mp_halftime 0",
            "mp_freezetime 0",
            "mp_respawn_on_death_ct 1",
            "mp_respawn_on_death_t 1",
            "mp_restartgame 1"
        ];
        foreach (var command in commands) Core.Engine.ExecuteCommand(command);
    }

    private string CurrentMap() => Core.ConVar.FindAsString("mapname")?.ValueAsString ?? string.Empty;

    private bool IsWinterMap()
    {
        var map = CurrentMap();
        return map.Equals("de_dust2", StringComparison.OrdinalIgnoreCase)
            || map.Equals("de_mirage", StringComparison.OrdinalIgnoreCase);
    }

    private void UpdateSnow()
    {
        try
        {
            if (!_snowEnabled || !IsWinterMap())
            {
                if (_emitters.Count > 0) ClearSnow();
                return;
            }

            var players = Core.PlayerManager.GetAllPlayers()
                .Where(p => p.IsValid && !p.IsFakeClient && p.IsAlive && p.Pawn?.AbsOrigin is not null)
                .Take(8)
                .ToList();

            var active = new HashSet<int>(players.Select(p => p.PlayerID));

            foreach (var id in _emitters.Keys.Where(id => !active.Contains(id)).ToArray())
                RemoveEmitter(id);

            foreach (var player in players)
            {
                var origin = player.Pawn?.AbsOrigin;
                if (origin is null) continue;

                if (!_emitters.TryGetValue(player.PlayerID, out var emitter) || !emitter.IsValidEntity)
                {
                    emitter = Core.EntitySystem.CreateEntity<CParticleSystem>();
                    emitter.EffectName = SnowEffect;
                    emitter.StartActive = true;
                    emitter.Active = true;
                    emitter.Teleport(new Vector(origin.Value.X, origin.Value.Y, origin.Value.Z + 128f), null, null);
                    emitter.DispatchSpawn();
                    _emitters[player.PlayerID] = emitter;
                }
                else
                {
                    emitter.Teleport(new Vector(origin.Value.X, origin.Value.Y, origin.Value.Z + 128f), null, null);
                }
            }
        }
        catch (Exception ex)
        {
            Core.Logger.LogWarning(ex, "Snow update failed.");
        }
    }

    private void RemoveEmitter(int playerId)
    {
        if (_emitters.TryGetValue(playerId, out var emitter))
        {
            try
            {
                if (emitter.IsValidEntity) emitter.Despawn();
            }
            catch { }
        }
        _emitters.Remove(playerId);
    }

    private void ClearSnow()
    {
        foreach (var id in _emitters.Keys.ToArray()) RemoveEmitter(id);
    }
}
