using System.Text.Json.Serialization;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Menu;

namespace AdminMapMenu;

public sealed class MapItem
{
    [JsonPropertyName("Name")] public string Name { get; set; } = "";
    [JsonPropertyName("Type")] public string Type { get; set; } = "standard";
    [JsonPropertyName("Value")] public string Value { get; set; } = "";
}

public sealed class AdminMapMenuConfig : BasePluginConfig
{
    [JsonPropertyName("AdminPermission")] public string AdminPermission { get; set; } = "@css/root";
    [JsonPropertyName("Maps")] public List<MapItem> Maps { get; set; } = new()
    {
        new() { Name = "❄ Dust2 Christmas", Type = "workshop", Value = "3620630831" },
        new() { Name = "❄ Mirage Winter", Type = "workshop", Value = "3359123851" },
        new() { Name = "Dust II", Type = "standard", Value = "de_dust2" },
        new() { Name = "Mirage", Type = "standard", Value = "de_mirage" },
        new() { Name = "Inferno", Type = "standard", Value = "de_inferno" },
        new() { Name = "Nuke", Type = "standard", Value = "de_nuke" },
        new() { Name = "Ancient", Type = "standard", Value = "de_ancient" },
        new() { Name = "Anubis", Type = "standard", Value = "de_anubis" },
        new() { Name = "Train", Type = "standard", Value = "de_train" },
        new() { Name = "Overpass", Type = "standard", Value = "de_overpass" }
    };
}

[MinimumApiVersion(376)]
public sealed class AdminMapMenuPlugin : BasePlugin, IPluginConfig<AdminMapMenuConfig>
{
    public override string ModuleName => "Winter Admin Map Menu";
    public override string ModuleVersion => "1.0.0";
    public override string ModuleAuthor => "OpenAI / Andrey";
    public override string ModuleDescription => "In-game admin menu for map changes and WinterSnow controls.";

    public AdminMapMenuConfig Config { get; set; } = new();

    public void OnConfigParsed(AdminMapMenuConfig config)
    {
        Config = config;
    }

    [ConsoleCommand("css_admin", "Открыть админ-меню")]
    [ConsoleCommand("css_wadmin", "Открыть зимнее админ-меню")]
    public void CommandAdmin(CCSPlayerController? player, CommandInfo info)
    {
        if (player is null || !player.IsValid)
        {
            info.ReplyToCommand("[WinterAdmin] Команда доступна игроку в игре.");
            return;
        }

        if (!AdminManager.PlayerHasPermissions(player, Config.AdminPermission))
        {
            player.PrintToChat(" [WinterAdmin] Нет доступа к админ-меню.");
            return;
        }

        OpenMainMenu(player);
    }

    private void OpenMainMenu(CCSPlayerController player)
    {
        var menu = new CenterHtmlMenu("❄ WINTER ADMIN MENU ❄", this)
        {
            TitleColor = "#7FDBFF",
            EnabledColor = "#FFFFFF",
            DisabledColor = "#777777",
            PrevPageColor = "#7FDBFF",
            NextPageColor = "#7FDBFF",
            CloseColor = "#FF6B6B",
            ExitButton = true
        };

        menu.AddMenuOption("🗺 Сменить карту", (p, _) => OpenMapMenu(p));
        menu.AddMenuOption("❄ Включить снег", (p, _) =>
        {
            Server.ExecuteCommand("css_snow on");
            p.PrintToChat(" [WinterAdmin] Снег включён.");
            OpenMainMenu(p);
        });
        menu.AddMenuOption("🌨 Выключить снег", (p, _) =>
        {
            Server.ExecuteCommand("css_snow off");
            p.PrintToChat(" [WinterAdmin] Снег выключен.");
            OpenMainMenu(p);
        });
        menu.AddMenuOption("🔄 Рестарт раунда", (p, _) =>
        {
            Server.ExecuteCommand("mp_restartgame 1");
            MenuManager.CloseActiveMenu(p);
        });

        menu.Open(player);
    }

    private void OpenMapMenu(CCSPlayerController player)
    {
        var menu = new CenterHtmlMenu("🗺 СМЕНА КАРТЫ", this)
        {
            TitleColor = "#7FDBFF",
            EnabledColor = "#FFFFFF",
            PrevPageColor = "#7FDBFF",
            NextPageColor = "#7FDBFF",
            CloseColor = "#FF6B6B",
            ExitButton = true
        };

        foreach (var item in Config.Maps)
        {
            var map = item;
            menu.AddMenuOption(map.Name, (p, _) =>
            {
                MenuManager.CloseActiveMenu(p);
                Server.PrintToChatAll($" [WinterAdmin] Администратор меняет карту на: {map.Name}");

                AddTimer(0.8f, () =>
                {
                    if (map.Type.Equals("workshop", StringComparison.OrdinalIgnoreCase))
                    {
                        Server.ExecuteCommand($"host_workshop_map {map.Value}");
                    }
                    else
                    {
                        Server.ExecuteCommand($"changelevel {map.Value}");
                    }
                });
            });
        }

        menu.AddMenuOption("⬅ Назад", (p, _) => OpenMainMenu(p));
        menu.Open(player);
    }
}
