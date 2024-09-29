using Dalamud.Interface.Windowing;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using Num = System.Numerics;

namespace StarlightBreaker
{
    public class ConfigWindow : Window, IDisposable
    {
        private Plugin Plugin;
        private ExcelSheet<UIColor> uiColours;

        private uint ButtonColor;
        private bool showColorPicker = false;


        private bool IsEnable;
        private Coloring Coloring;
        private uint Color;
        private bool Italics;

        private Configuration Configuration;

        public ConfigWindow(Plugin plugin):base(Plugin.Name)
        {
            Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoResize;
            Size = new Num.Vector2(400, 220);
            this.Plugin = plugin;
            this.uiColours = Plugin.DataManager.Excel.GetSheet<UIColor>();
            this.IsEnable = plugin.Configuration.Enable;
            this.Italics=plugin.Configuration.Italics;
            this.Color=plugin.Configuration.Color;
            this.Coloring = plugin.Configuration.Coloring;
            ButtonColor = uiColours.GetRow(this.Color).UIForeground;
        }
        public override void Draw()
        {

            ImGui.Checkbox("Enable", ref IsEnable);

            ImGui.Text("Coloring");
            ImGui.SameLine();
            if (ImGui.BeginTable("ColoringRadioButton", 2))
            {
                foreach (Coloring z in Enum.GetValues(typeof(Coloring)))
                {
                    ImGui.TableNextColumn();
                    if (ImGui.RadioButton(z.ToString(), this.Coloring == z))
                    {
                        this.Coloring = z;
                    }
                }
                ImGui.EndTable();
            }

            ImGui.Checkbox("Italics", ref Italics);

            ImGui.Text("Color For Profanitay Words");
            ImGui.SameLine();
            var temp = BitConverter.GetBytes(ButtonColor);
            if (ImGui.ColorButton("Choose Color", new Num.Vector4(
                (float)temp[3] / 255,
                (float)temp[2] / 255,
                (float)temp[1] / 255,
                (float)temp[0] / 255))) {
                showColorPicker = true;
            }

            if (ImGui.Button("Save")) {
                UpdateConfig();
            }
            ImGui.SameLine();

            if (showColorPicker) {
                ShowColorPicker();
            }
        }

        private void UpdateConfig()
        {
            this.Plugin.Configuration.Color = Color;
            this.Plugin.Configuration.Italics = Italics;
            this.Plugin.Configuration.Enable = IsEnable;
            this.Plugin.Configuration.Coloring = this.Coloring;
            this.Plugin.Configuration.Save();
        }

        private void ShowColorPicker()
        {
            //ImGui.SetNextWindowSizeConstraints(new Num.Vector2(320, 440), new Num.Vector2(640, 880));
            ImGui.SetNextWindowSize(new Num.Vector2(320, 500));
            ImGui.Begin("UIColor Picker", ref showColorPicker, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNav| ImGuiWindowFlags.NoResize);
            ImGui.Columns(10, "##columnsID", false);
            foreach (var z in uiColours) {
                var temp = BitConverter.GetBytes(z.UIForeground);
                if (ImGui.ColorButton(z.RowId.ToString(), new Num.Vector4(
                    (float)temp[3] / 255,
                    (float)temp[2] / 255,
                    (float)temp[1] / 255,
                    (float)temp[0] / 255))) {
                    this.ButtonColor = z.UIForeground;
                    this.Color = z.RowId;
                    showColorPicker = false;
                }

                ImGui.NextColumn();
            }

            ImGui.Columns(1);
            ImGui.End();
        }

        public void Dispose() { }

    }
}
