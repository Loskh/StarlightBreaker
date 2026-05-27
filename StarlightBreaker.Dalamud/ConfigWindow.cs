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

        public ConfigWindow(Plugin plugin) : base(Plugin.Name, ImGuiWindowFlags.AlwaysAutoResize)
        {
            //Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoResize;
            SizeConstraints = new WindowSizeConstraints { MinimumSize = new Num.Vector2(400, 300) };
            this.Plugin = plugin;
            this.config = this.Plugin.Configuration;
        }
        public override void Draw()
        {
            if (this.ShowUpdateTips)
            {
                ImGui.Text("如果遇到招募崩溃,可以禁用招募板设置,并通过插件旁的反馈按钮进行反馈");
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
                    //ImGui.Text("暂时禁用");
                    needSave |= ImGui.Checkbox("启用##PartyFinder", ref this.config.PartyFinderConfig.Enable);
                    //ImGui.Text("如果遇到招募崩溃,可以禁用该功能");
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
                Plugin.PluginLog.Info("保存设置 / Saving config");
                this.Plugin.Configuration.Save();
            }
        }

        public void Dispose() { }

    }
}
