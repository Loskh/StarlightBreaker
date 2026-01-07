using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel;
using Lumina.Excel.Sheets;
using System;
using Num = System.Numerics;

namespace StarlightBreaker
{
    public class ConfigWindow : Window, IDisposable
    {
        private Plugin Plugin;

        private Configuration config;

        internal bool ShowUpdateTips = false;

        public ConfigWindow(Plugin plugin) : base(Plugin.Name, ImGuiWindowFlags.NoResize)
        {
            //Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoResize;
            Size = new Num.Vector2(400, 300);
            this.Plugin = plugin;
            this.config = this.Plugin.Configuration;
        }
        public override void Draw()
        {
            if (this.ShowUpdateTips)
            {
                ImGui.Text("由于版本更新，旧设置失效，请重新设置。");
                ImGui.Text("新版本已解决未开反屏蔽的玩家会看到空白招募的问题");
                ImGui.Text("当系统提示处理招募失败时，请检查招募文本的词语");
            }
            var needSave = false;
            if (ImGui.CollapsingHeader("聊天栏设置", ImGuiTreeNodeFlags.DefaultOpen))
            {
                using (ImRaii.Group())
                {
                    needSave |= ImGui.Checkbox("启用##Chat", ref this.config.ChatLogConfig.Enable);
                    needSave |= ImGui.Checkbox("特殊显示##Chat", ref this.config.ChatLogConfig.EnableColor);
                }
            }

            if (ImGui.CollapsingHeader("招募板设置", ImGuiTreeNodeFlags.DefaultOpen))
            {
                using (ImRaii.Group())
                {
                    needSave |= ImGui.Checkbox("启用##PartyFinder", ref this.config.PartyFinderConfig.Enable);
                    ImGui.Text("由于接收到一些导致招募崩溃的反馈，暂时禁用该功能");
                    needSave |= ImGui.Checkbox("特殊显示##PartyFinder", ref this.config.PartyFinderConfig.EnableColor);
                }
            }
            if (ImGui.CollapsingHeader("特殊显示设置", ImGuiTreeNodeFlags.DefaultOpen))
            {
                using (ImRaii.Group())
                {
                    needSave |= ImGui.Checkbox("斜体", ref this.config.FontConfig.Italics);
                    needSave |= ImGui.Checkbox("颜色", ref this.config.FontConfig.EnableColor);
                    ImGui.SameLine();
                    needSave |= ImGuiExt.UiColorPicker($"##picker_default", ref this.config.FontConfig.Color);
                }
            }

            if (needSave)
            {
                Plugin.PluginLog.Info("Saving config");
                this.Plugin.Configuration.Save();
            }
        }

        public void Dispose() { }

    }
}
