using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using Num = System.Numerics;

namespace StarlightBreaker
{
    class PluginUI
    {
        public bool IsVisible = false;
        public bool IsEnable= true;
        public bool IsEnableColorWords = true;
        private bool showColorPicker = false;
        private Plugin Plugin;
        private ExcelSheet<UIColor> uiColours;
        private uint ButtonColor;
        private uint Color;
        public PluginUI(Plugin plugin)
        {
            this.Plugin = plugin;
            this.uiColours = plugin.pluginInterface.Data.Excel.GetSheet<UIColor>();
            this.IsEnable = plugin.configuration.Enable;
            ButtonColor = uiColours.GetRow(this.Plugin.configuration.Color).UIForeground;
        }
        public void Draw()
        {
            if (!IsVisible)
                return;
            ImGui.Begin("StarLightBreaker");
            ImGui.Checkbox("Enable", ref IsEnable);

            ImGui.Checkbox("Color For Profanitay Words in ChatLog",ref IsEnableColorWords);
            ImGui.SameLine();
            var temp = BitConverter.GetBytes(ButtonColor);
            if (ImGui.ColorButton("Choose Color", new Num.Vector4(
                (float)temp[3] / 255,
                (float)temp[2] / 255,
                (float)temp[1] / 255,
                (float)temp[0] / 255)))
            {
                showColorPicker = true;
            }
            if (ImGui.Button("Save"))
            {
                UpdateConfig();
            }
            ImGui.SameLine();
            if (ImGui.Button("Save&Close"))
            {
                UpdateConfig();
                this.IsVisible = false;
            }

            ImGui.End();
            if (showColorPicker)
            {
                ShowColorPicker();
            }
            return;
        }
        private void UpdateConfig() {
            this.Plugin.configuration.Color = Color;
            this.Plugin.configuration.Enable = IsEnable;
            this.Plugin.configuration.EnableColorWords = IsEnableColorWords;
            this.Plugin.UpdataPatch();
            this.Plugin.configuration.Save();
    }
        private void ShowColorPicker()
        {
            ImGui.SetNextWindowSizeConstraints(new Num.Vector2(320, 440), new Num.Vector2(640, 880));
            ImGui.Begin("UIColor Picker", ref showColorPicker);
            ImGui.Columns(10, "##columnsID", false);
            foreach (var z in uiColours)
            {
                var temp = BitConverter.GetBytes(z.UIForeground);
                if (ImGui.ColorButton(z.RowId.ToString(), new Num.Vector4(
                    (float)temp[3] / 255,
                    (float)temp[2] / 255,
                    (float)temp[1] / 255,
                    (float)temp[0] / 255)))
                {
                    this.ButtonColor = z.UIForeground;
                    this.Color = z.RowId;
                    showColorPicker = false;
                }

                ImGui.NextColumn();
            }

            ImGui.Columns(1);
            ImGui.End();
        }

    }
}
