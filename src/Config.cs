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
    public uint FieldIconId = 60561;
    public uint QuestIconId = 71005;
    public int MarkerRadius = 30;

    public void Save() => Plugin.PluginInterface.SavePluginConfig(this);

    public static Config Load()
        => Plugin.PluginInterface.GetPluginConfig() as Config ?? new Config();
}
