using System;
using Dalamud.Configuration;

namespace SimpleAether;

[Serializable]
public sealed class Config : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled = true;
    public bool ShowFieldCurrents = true;
    public bool ShowQuestCurrents = true;

    /// Marqueur de vent ethere du jeu. 0 = losange vert trace par l'overlay.
    public uint FieldIconId = 60033;

    public uint QuestIconId = 60013;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    public static Config Load()
        => Plugin.PluginInterface.GetPluginConfig() as Config ?? new Config();
}
