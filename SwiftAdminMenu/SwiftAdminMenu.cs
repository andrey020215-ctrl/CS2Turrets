using Microsoft.Extensions.Logging;
using SwiftlyS2.Core.Menus.OptionsBase;
using SwiftlyS2.Shared;
using SwiftlyS2.Shared.Commands;
using SwiftlyS2.Shared.Menus;
using SwiftlyS2.Shared.Players;
using SwiftlyS2.Shared.Plugins;

namespace SwiftAdminMenu;

[PluginMetadata(
    Id = "andrey.swiftadminmenu",
    Version = "1.0.0",
    Name = "Andrey Admin Menu",
    Author = "OpenAI / Andrey",
    Description = "Compact SwiftlyS2 admin menu for maps, turret menu and 60-minute test round")]
public sealed class SwiftAdminMenu : BasePlugin
{
    private const string WinterNukeWorkshop = "3620133293";

    public SwiftAdminMenu(ISwiftlyCore core) : base(core) { }

    public override void Load(bool hotReload)
    {
        Core.Logger.LogInformation("Andrey Admin Menu loaded. Use !admin or sw_admin.");
    }

    public override void Unload() { }

    [Command("admin", helpText: "Open server admin menu")]
    public void AdminCommand(ICommandContext context)
    {
        if (!context.IsSentByPlayer || context.Sender is null)
        {
            context.Reply("Use sw_admin from a connected player or !admin in chat.");
            return;
        }

        ShowMainMenu(context.Sender);
    }

    private void ShowMainMenu(IPlayer player)
    {
        var builder = Core.MenusAPI.CreateBuilder();
        builder.Design.SetMenuTitle("SERVER ADMIN");

        AddAction(builder, "❄ Winter Nuke", player, () => RunServerCommand($"host_workshop_map {WinterNukeWorkshop}"));
        AddAction(builder, "Nuke", player, () => RunServerCommand("changelevel de_nuke"));
        AddAction(builder, "Inferno", player, () => RunServerCommand("changelevel de_inferno"));
        AddAction(builder, "Mirage", player, () => RunServerCommand("changelevel de_mirage"));
        AddAction(builder, "Turret menu", player, () => player.ExecuteCommand("sw_turret"));
        AddAction(builder, "60 min round", player, Apply60MinuteRound);
        AddAction(builder, "Restart round", player, () => RunServerCommand("mp_restartgame 1"));
        AddAction(builder, "Reload turret assets", player, () =>
        {
            RunServerCommand("mm_download_addon 3618032051");
            player.SendChat("[ADMIN] Turret Workshop addon download requested. Reload the map after download.");
        });

        Core.MenusAPI.OpenMenuForPlayer(player, builder.Build());
    }

    private void AddAction(IMenuBuilderAPI builder, string text, IPlayer player, Action action)
    {
        var option = new ButtonMenuOption(text);
        option.Click += (sender, args) =>
        {
            Core.Scheduler.NextTick(() =>
            {
                action();
                var current = Core.MenusAPI.GetCurrentMenu(args.Player);
                if (current is not null)
                    Core.MenusAPI.CloseMenuForPlayer(args.Player, current);
            });
            return ValueTask.CompletedTask;
        };
        builder.AddOption(option);
    }

    private void Apply60MinuteRound()
    {
        RunServerCommand("mp_do_warmup_period 0");
        RunServerCommand("mp_warmuptime 0");
        RunServerCommand("mp_warmup_end");
        RunServerCommand("mp_ignore_round_win_conditions 0");
        RunServerCommand("mp_roundtime 60");
        RunServerCommand("mp_roundtime_defuse 60");
        RunServerCommand("mp_roundtime_hostage 60");
        RunServerCommand("mp_timelimit 0");
        RunServerCommand("mp_maxrounds 0");
        RunServerCommand("mp_halftime 0");
        RunServerCommand("mp_freezetime 0");
        RunServerCommand("mp_respawn_on_death_ct 1");
        RunServerCommand("mp_respawn_on_death_t 1");
        RunServerCommand("mp_restartgame 1");
    }

    private void RunServerCommand(string command)
    {
        Core.Engine.ExecuteCommand(command);
    }
}
