using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.Command;
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

    private const string CommandName = "/saether";

    private readonly CurrentResolver resolver = new();
    private readonly MarkerPoster poster = new();
    private readonly Config config;
    private readonly ConfigWindow configWindow;

    private DateTime lastTick = DateTime.MinValue;
    private HashSet<(uint, TargetKind)> lastPosted = new();
    private bool lastMapVisible;
    private (uint, uint, int) lastStyle;

    public Plugin()
    {
        config = Config.Load();
        configWindow = new ConfigWindow(config);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "SimpleAether. Sous-commandes : dump, calibrate, config",
        });
        Log.Information("SimpleAether charge.");

        Framework.Update += OnFrameworkUpdate;
        ClientState.TerritoryChanged += OnTerritoryChanged;
        ClientState.Login += OnLogin;

        PluginInterface.UiBuilder.Draw += configWindow.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += OpenConfig;
    }

    private void OpenConfig() => configWindow.IsOpen = true;

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "dump":
                DumpCurrentZone();
                break;
            case "calibrate":
                CalibrateMarker();
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
            Log.Information(
                $"  [{t.Kind}] id={t.AetherCurrentId} " +
                $"monde=({t.WorldPos.X:0.0}, {t.WorldPos.Y:0.0}, {t.WorldPos.Z:0.0}) " +
                $"carte=({coord.X:0.0}, {coord.Y:0.0}) \"{t.Label}\"");
        }
    }

    private void CalibrateMarker()
    {
        var player = ObjectTable.LocalPlayer;
        if (player == null)
        {
            Log.Warning("calibration : pas de joueur local.");
            return;
        }
        var map = DataManager.GetExcelSheet<TerritoryType>()
            ?.GetRowOrDefault(ClientState.TerritoryType)?.Map.ValueNullable;
        if (map is not { } m)
        {
            Log.Warning("calibration : carte introuvable pour ce territoire.");
            return;
        }
        poster.PostCalibrationMarker(player.Position, m.SizeFactor, m.OffsetX, m.OffsetY,
            config.FieldIconId, config.MarkerRadius);
    }

    private void OnTerritoryChanged(uint territoryId)
    {
        resolver.InvalidateCache();
        lastPosted = new HashSet<(uint, TargetKind)>();
        poster.Clear();
    }

    private void OnLogin()
    {
        resolver.InvalidateCache();
        lastPosted = new HashSet<(uint, TargetKind)>();
    }

    private bool AreaMapVisible()
    {
        var addon = GameGui.GetAddonByName("AreaMap");
        return !addon.IsNull && addon.IsVisible;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        try
        {
            if (DateTime.UtcNow - lastTick < TimeSpan.FromSeconds(1)) return;
            lastTick = DateTime.UtcNow;

            if (!config.Enabled)
            {
                if (poster.HasPosted)
                {
                    poster.Clear();
                    lastPosted = new HashSet<(uint, TargetKind)>();
                }
                return;
            }

            if (!ClientState.IsLoggedIn || ObjectTable.LocalPlayer == null) return;

            var mapVisible = AreaMapVisible();
            var mapJustOpened = mapVisible && !lastMapVisible;
            lastMapVisible = mapVisible;

            var targets = resolver.Resolve(ClientState.TerritoryType)
                .Where(t => t.Kind == TargetKind.Field ? config.ShowFieldCurrents : config.ShowQuestCurrents)
                .ToList();
            var signature = new HashSet<(uint, TargetKind)>(
                targets.Select(t => (t.AetherCurrentId, t.Kind)));

            var style = (config.FieldIconId, config.QuestIconId, config.MarkerRadius);
            if (!mapJustOpened && style == lastStyle && signature.SetEquals(lastPosted)) return;

            if (targets.Count == 0)
            {
                poster.Clear();
                lastStyle = style;
                lastPosted = signature;
                return;
            }

            var map = DataManager.GetExcelSheet<TerritoryType>()
                ?.GetRowOrDefault(ClientState.TerritoryType)?.Map.ValueNullable;
            if (map is not { } m) return;   // pas d'etat mis a jour -> retente au tick suivant

            if (poster.Post(targets, m.SizeFactor, m.OffsetX, m.OffsetY,
                config.FieldIconId, config.QuestIconId, config.MarkerRadius))
            {
                lastStyle = style;
                lastPosted = signature;
            }
        }
        catch (Exception e)
        {
            Log.Error(e, "tick SimpleAether");
        }
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.OpenConfigUi -= OpenConfig;
        PluginInterface.UiBuilder.Draw -= configWindow.Draw;
        ClientState.Login -= OnLogin;
        ClientState.TerritoryChanged -= OnTerritoryChanged;
        Framework.Update -= OnFrameworkUpdate;
        CommandManager.RemoveHandler(CommandName);
        try { poster.Clear(); } catch (Exception e) { Log.Error(e, "clear markers on unload"); }
    }
}
