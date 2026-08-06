using System;
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
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/saether";

    private readonly CurrentResolver resolver = new();

    public Plugin()
    {
        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "SimpleAether. Sous-commandes : dump",
        });
        Log.Information("SimpleAether charge.");
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "dump":
                DumpCurrentZone();
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

    public void Dispose()
    {
        CommandManager.RemoveHandler(CommandName);
    }
}
