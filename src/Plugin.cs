using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Command;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace SimpleAether;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;

    private const string CommandName = "/saether";

    private readonly CurrentResolver resolver = new();
    private readonly MapOverlay overlay;
    private readonly Config config;
    private readonly ConfigWindow configWindow;

    private DateTime lastTick = DateTime.MinValue;

    public Plugin()
    {
        config = Config.Load();
        configWindow = new ConfigWindow(config);
        overlay = new MapOverlay(config);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "SimpleAether. Sous-commandes : dump, config",
        });
        Log.Information("SimpleAether charge.");

        Framework.Update += OnFrameworkUpdate;
        ClientState.TerritoryChanged += OnTerritoryChanged;
        ClientState.Login += OnLogin;

        PluginInterface.UiBuilder.Draw += configWindow.Draw;
        PluginInterface.UiBuilder.Draw += overlay.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
    }

    /// <summary>
    /// GetFromGameIcon leve une IconNotFoundException sur un id inexistant, et la
    /// numerotation des icones est trouee. Tout passe donc par ici : un id absent
    /// vaut "rien a dessiner", pas une exception en pleine frame.
    /// </summary>
    public static IDalamudTextureWrap? Icon(uint iconId) =>
        iconId != 0 && TextureProvider.TryGetFromGameIcon(new GameIconLookup(iconId), out var shared)
            ? shared.GetWrapOrDefault()
            : null;

    private void OpenConfig() => configWindow.IsOpen = true;

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "dump":
                DumpCurrentZone();
                break;
            case "config":
                configWindow.IsOpen = true;
                break;
            default:
                Log.Information($"SimpleAether actif. Territoire courant : {ClientState.TerritoryType}");
                break;
        }
    }

    private void DumpCurrentZone()
    {
        var territoryId = ClientState.TerritoryType;
        var targets = resolver.Resolve(territoryId);
        Log.Information($"=== dump territoire {territoryId} : {targets.Count} cible(s) ===");

        var terr = DataManager.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territoryId);
        var map = terr?.Map.ValueNullable;

        foreach (var t in targets)
        {
            var coord = map is { } m
                ? MapCoords.WorldToMap(t.WorldPos, m.SizeFactor, m.OffsetX, m.OffsetY)
                : new Vector2(float.NaN, float.NaN);
            var iconId = MapOverlay.IconFor(t, config);
            var texture = Icon(iconId);
            Log.Information(
                $"  [{t.Kind}] id={t.AetherCurrentId} icone={iconId} " +
                $"texture={(texture != null ? "chargee" : "NULL")} " +
                $"monde=({t.WorldPos.X:0.0}, {t.WorldPos.Y:0.0}, {t.WorldPos.Z:0.0}) " +
                $"carte=({coord.X:0.0}, {coord.Y:0.0}) \"{t.Label}\"");
        }
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        resolver.InvalidateCache();
        overlay.Targets = [];
    }

    private void OnLogin() => resolver.InvalidateCache();

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            if (DateTime.UtcNow - lastTick < TimeSpan.FromSeconds(1)) return;
            lastTick = DateTime.UtcNow;

            // L'overlay redessine a chaque frame depuis cette liste : il suffit de la
            // tenir a jour. Plus de gate anti-repost -- elle n'existait que pour eviter
            // de reposter des marqueurs de jeu et de rouvrir la carte a chaque fois.
            overlay.Targets = config.Enabled && ClientState.IsLoggedIn && ObjectTable.LocalPlayer != null
                ? resolver.Resolve(ClientState.TerritoryType)
                    .Where(t => t.Kind == TargetKind.Field ? config.ShowFieldCurrents : config.ShowQuestCurrents)
                    .ToList()
                : [];
        }
        catch (Exception e)
        {
            Log.Error(e, "tick SimpleAether");
        }
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        PluginInterface.UiBuilder.Draw -= overlay.Draw;
        PluginInterface.UiBuilder.Draw -= configWindow.Draw;
        ClientState.Login -= OnLogin;
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        Framework.Update -= OnFrameworkUpdate;
        CommandManager.RemoveHandler(CommandName);
    }
}
