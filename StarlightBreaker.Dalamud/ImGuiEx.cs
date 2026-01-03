using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using FFXIVClientStructs.FFXIV.Client.UI;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

// https://github.com/Caraxi/SimpleTweaksPlugin/blob/main/Utility/ImGuiExt.cs
namespace StarlightBreaker
{
    public static class ImGuiExt
    {
        public static Vector4 UiColorToVector4(uint col)
        {
            var fa = col & 255;
            var fb = (col >> 8) & 255;
            var fg = (col >> 16) & 255;
            var fr = (col >> 24) & 255;
            return new Vector4(fr / 255f, fg / 255f, fb / 255f, fa / 255f);
        }

        public static Vector3 UiColorToVector3(uint col)
        {
            var fb = (col >> 8) & 255;
            var fg = (col >> 16) & 255;
            var fr = (col >> 24) & 255;
            return new Vector3(fr / 255f, fg / 255f, fb / 255f);
        }


        private static List<UIColor> uniqueSortedUiForeground;
        private static List<UIColor> uniqueSortedUiGlow;

        private static void BuildUiColorLists()
        {
            uniqueSortedUiForeground = new List<UIColor>();
            uniqueSortedUiGlow = new List<UIColor>();
            var s = Plugin.DataManager.Excel.GetSheet<UIColor>();
            if (s == null) return;
            foreach (var c in s)
            {
                if (uniqueSortedUiForeground.All(u => u.Dark != c.Dark))
                {
                    uniqueSortedUiForeground.Add(c);
                }
                if (uniqueSortedUiGlow.All(u => u.Light != c.Light))
                {
                    uniqueSortedUiGlow.Add(c);
                }
            }

            uniqueSortedUiForeground.Sort((a, b) =>
            {
                var aRgb = UiColorToVector4(a.Dark);
                var bRgb = UiColorToVector4(b.Dark);
                float aH = 0f, aS = 0f, aV = 0f, bH = 0f, bS = 0f, bV = 0f;
                ImGui.ColorConvertRGBtoHSV(aRgb.X, aRgb.Y, aRgb.Z, ref aH, ref aS, ref aV);
                ImGui.ColorConvertRGBtoHSV(bRgb.X, bRgb.Y, bRgb.Z, ref bH, ref bS, ref bV);
                if (aH < bH) return -1;
                if (aH > bH) return 1;
                if (aS < bS) return -1;
                if (aS > bS) return 1;
                if (aV < bV) return -1;
                if (aV > bV) return 1;
                return 0;
            });

            uniqueSortedUiGlow.Sort((a, b) =>
            {
                var aRgb = UiColorToVector4(a.Light);
                var bRgb = UiColorToVector4(b.Light);
                float aH = 0f, aS = 0f, aV = 0f, bH = 0f, bS = 0f, bV = 0f;
                ImGui.ColorConvertRGBtoHSV(aRgb.X, aRgb.Y, aRgb.Z, ref aH, ref aS, ref aV);
                ImGui.ColorConvertRGBtoHSV(bRgb.X, bRgb.Y, bRgb.Z, ref bH, ref bS, ref bV);
                if (aH < bH) return -1;
                if (aH > bH) return 1;
                if (aS < bS) return -1;
                if (aS > bS) return 1;
                if (aV < bV) return -1;
                if (aV > bV) return 1;
                return 0;
            });

        }

        public enum ColorPickerMode
        {
            ForegroundOnly,
            GlowOnly,
            ForegroundAndGlow,
        }

        public static bool UiColorPicker(string label, ref ushort colourKey, ColorPickerMode mode = ColorPickerMode.ForegroundOnly)
        {
            var modified = false;

            var glowOnly = mode == ColorPickerMode.GlowOnly;

            var colorSheet = Plugin.DataManager.Excel.GetSheet<UIColor>();

            if (!colorSheet.TryGetRow(colourKey, out var currentColor))
            {
                if (!colorSheet.TryGetRow(0, out currentColor))
                {
                    return false;
                }
            }

            var id = ImGui.GetID(label);

            ImGui.SetNextItemWidth(24 * ImGui.GetIO().FontGlobalScale);

            ImGui.PushStyleColor(ImGuiCol.FrameBg, UiColorToVector4(glowOnly ? currentColor.Light : currentColor.Dark));
            ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, UiColorToVector4(glowOnly ? currentColor.Light : currentColor.Dark));
            ImGui.PushStyleColor(ImGuiCol.FrameBgActive, UiColorToVector4(glowOnly ? currentColor.Light : currentColor.Dark));

            if (mode == ColorPickerMode.ForegroundAndGlow)
            {
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 4);
                ImGui.PushStyleColor(ImGuiCol.Border, UiColorToVector4(currentColor.Light));
            }

            var comboOpen = ImGui.BeginCombo($"{label}##combo", string.Empty, ImGuiComboFlags.NoArrowButton);
            ImGui.PopStyleColor(3);
            if (mode == ColorPickerMode.ForegroundAndGlow)
            {
                ImGui.PopStyleVar();
                ImGui.PopStyleColor();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            }

            if (comboOpen)
            {

                if (uniqueSortedUiForeground == null || uniqueSortedUiGlow == null) BuildUiColorLists();

                var cl = (glowOnly ? uniqueSortedUiGlow : uniqueSortedUiForeground) ?? new List<UIColor>();

                var sqrt = (int)Math.Sqrt(cl.Count);
                ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(1));
                for (var i = 0; i < cl.Count; i++)
                {
                    var c = cl[i];
                    if (i != 0 && i % sqrt != 0) ImGui.SameLine();
                    if (ImGui.ColorButton($"##ColorPick_{i}_{c.RowId}", UiColorToVector4(glowOnly ? c.Light : c.Dark), ImGuiColorEditFlags.NoTooltip))
                    {
                        colourKey = (ushort)c.RowId;
                        modified = true;
                        ImGui.CloseCurrentPopup();
                    }
                }
                ImGui.PopStyleVar();


                ImGui.EndCombo();
            }

            return modified;
        }
    }
}
