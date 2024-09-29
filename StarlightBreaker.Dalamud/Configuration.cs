using Dalamud.Configuration;
using System;

namespace StarlightBreaker
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool Enable { get; set; } = true;
        public Coloring Coloring = Coloring.ChatLogOnly;
        public uint Color = 17;
        public bool Italics = false;
        // the below exist just to make saving less cumbersome

        public void Save()
        {
            Plugin.PluginInterface.SavePluginConfig(this);
        }
    }
}
