using Dalamud.Configuration;
using System;

namespace StarlightBreaker
{
    [Serializable]

    public class ChatLogConfig
    {
        public bool Enable = true;
        public bool EnableColor = false;
    }
    [Serializable]
    public class PartyFinderConfig
    {
        public bool Enable = true;
        public bool EnableColor = false;
    }
    [Serializable]
    public class FontConfig
    {
        public ushort Color = 17;
        public bool Italics = false;
        public bool EnableColor = false;
    }
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public ChatLogConfig ChatLogConfig { get; set; } = new();
        public PartyFinderConfig PartyFinderConfig { get; set; } = new();
        public FontConfig FontConfig { get; set; } = new();


        public void Save()
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
    }
}
