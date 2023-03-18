using Dalamud.Game.Text;
using ImGuiNET;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using System;
using System.Collections.Generic;
using System.Linq;
using Num = System.Numerics;

namespace StarlightBreaker
{
    class PluginUI
    {
        private Plugin Plugin;
        private ExcelSheet<UIColor> uiColours;

        private uint ButtonColor;
        private bool showColorPicker = false;
        public bool IsVisible = false;


        private bool IsEnable;
        private Coloring Coloring;
        private uint Color;
        private bool Italics;

        internal List<ushort> FilterChannels;

        public PluginUI(Plugin plugin)
        {
            this.Plugin = plugin;
            this.uiColours = plugin.DataManager.Excel.GetSheet<UIColor>();
            this.IsEnable = plugin.Configuration.Enable;
            this.Italics=plugin.Configuration.Italics;
            this.Color=plugin.Configuration.Color;
            this.Coloring = plugin.Configuration.Coloring;
            ButtonColor = uiColours.GetRow(this.Color).UIForeground;
        }
        public void Draw()
        {
            if (!IsVisible)
                return;

            ImGui.SetNextWindowSize(new Num.Vector2(500, 300));
            ImGui.Begin("StarLightBreaker", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoResize);

            ImGui.Checkbox("Enable", ref IsEnable);

            ImGui.Text("Coloring");
            ImGui.SameLine();
            if (ImGui.RadioButton("None", Coloring == Coloring.None))
                Coloring = Coloring.None;
            ImGui.SameLine();
            if (ImGui.RadioButton("ChatLogOnly", Coloring == Coloring.ChatLogOnly))
                Coloring = Coloring.ChatLogOnly;
            ImGui.SameLine();
            if (ImGui.RadioButton("ChatLogOnlyMyself", Coloring == Coloring.ChatLogOnlyMyself))
                Coloring = Coloring.ChatLogOnlyMyself;

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

            if (Coloring != Coloring.None)
            {
                if (ImGui.Button("Select All"))
                    SetAllFilterChannels(true);
                ImGui.SameLine();
                if (ImGui.Button("Reset"))
                    SetAllFilterChannels(false);
                DrawFilters();
            }

            if (ImGui.Button("Save")) {
                UpdateConfig();
            }
            ImGui.SameLine();

            if (ImGui.Button("Save&Close")) {
                UpdateConfig();
                this.IsVisible = false;
            }

            ImGui.End();
            if (showColorPicker) {
                ShowColorPicker();
            }
            return;
        }

        private void UpdateConfig()
        {
            this.Plugin.Configuration.Color = Color;
            this.Plugin.Configuration.Italics = Italics;
            this.Plugin.Configuration.Enable = IsEnable;
            this.Plugin.Configuration.Coloring = this.Coloring;
            this.Plugin.Configuration.FilterChannels = this.FilterChannels;
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

        private void DrawFilters()
        {
            ImGui.Columns(3, "FiltersTable", true);
            foreach (ushort chatType in Enum.GetValues(typeof(XivChatType)))
            {
                if (this.Plugin.ChatPassedList.Contains((XivChatType)chatType)) continue;
                string chatTypeName = Enum.GetName(typeof(XivChatType), chatType);
                bool checkboxClicked = this.FilterChannels.Contains(chatType);
                if (ImGui.Checkbox(chatTypeName + "##filter", ref checkboxClicked))
                {
                    if (checkboxClicked)
                    {
                        this.FilterChannels.Add(chatType);
                    }
                    else
                    {
                        this.FilterChannels.Remove(chatType);
                    }
                    this.FilterChannels.Sort();
                }
                ImGui.NextColumn();
            }
            ImGui.Columns(1);
        }

        private void SetAllFilterChannels(bool cond)
        {
            
            if (!cond)
            {
                this.FilterChannels= new List<ushort>();
                return;
            }
            var tempFilterChannels = new List<ushort>();
            foreach (ushort chatType in Enum.GetValues(typeof(XivChatType)))
            {
                if (this.Plugin.ChatPassedList.Contains((XivChatType)chatType)) continue;
                tempFilterChannels.Add(chatType);

            }
            tempFilterChannels.Sort();
            this.FilterChannels = tempFilterChannels;
        }

    }
}
