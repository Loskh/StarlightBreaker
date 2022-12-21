using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.IoC;
using Dalamud.Logging;
using Dalamud.Plugin;
using UTF8String = FFXIVClientStructs.FFXIV.Client.System.String.Utf8String;

namespace StarlightBreaker {
    public class Plugin : IDalamudPlugin {
        public string Name => "StarlightBreaker";

        [PluginService]
        [RequiredVersion("1.0")]
        private DalamudPluginInterface PluginInterface { get; set; }
        [PluginService]
        [RequiredVersion("1.0")]
        private SigScanner Scanner { get; set; }
        [PluginService]
        [RequiredVersion("1.0")]
        private CommandManager CommandManager { get; set; }
        [PluginService]
        [RequiredVersion("1.0")]
        private ClientState ClientState { get; set; }
        [PluginService]
        [RequiredVersion("1.0")]
        private ChatGui ChatGui { get; set; }
        [PluginService]
        [RequiredVersion("1.0")]
        internal DataManager DataManager { get; set; }
        [PluginService]
        [RequiredVersion("1.0")]
        internal Framework Framework { get; set; }

        private PluginUI PluginUi { get; set; }
        internal Configuration Configuration { get; set; }

        private readonly IntPtr VulgarInstance = IntPtr.Zero;
        private readonly IntPtr VulgarPartyInstance = IntPtr.Zero;


        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate void FilterSeStringDelegate(IntPtr vulgarInstance, FFXIVClientStructs.FFXIV.Client.System.String.Utf8String utf8String);
        private Hook<FilterSeStringDelegate> FilterSeStringHook;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate bool VulgarCheckDelegate(IntPtr vulgarInstance, FFXIVClientStructs.FFXIV.Client.System.String.Utf8String utf8String);
        private Hook<VulgarCheckDelegate> VulgarCheckHook;

        public Plugin() {
            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);
            this.PluginUi = new PluginUI(this);
#if DEBUG
            DrawConfigUI();
#endif

            try {
                // 48 8B 0D ?? ?? ?? ?? 48 8B 81 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B D3
                VulgarInstance = Marshal.ReadIntPtr(Framework.Address.BaseAddress + 0x2B40);
                VulgarPartyInstance = Marshal.ReadIntPtr(Framework.Address.BaseAddress + 0x2B40 + 0x8);

                if (this.Scanner.TryScanText("E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 ?? 5B C3 ?? ?? ?? ?? ?? ?? ?? 48 83 EC ?? 48 8B CA", out var ptr0)) {
                    this.FilterSeStringHook = new Hook<FilterSeStringDelegate>(ptr0, this.FilterSeStringDetour);
                }
                this.FilterSeStringHook?.Enable();

                if (this.Scanner.TryScanText("E8 ?? ?? ?? ?? 84 C0 74 16 48 8D 15 ?? ?? ?? ??", out var ptr1)) {
                    this.VulgarCheckHook = new Hook<VulgarCheckDelegate>(ptr1, this.VulgarCheckDetour);
                }
                this.VulgarCheckHook?.Enable();

                this.ChatGui.ChatMessage += Chat_OnChatMessage;
            }
            catch (Exception ex) {
                PluginLog.Error(ex, "开启屏蔽词染色失败", Array.Empty<object>());
            }

            this.PluginInterface.UiBuilder.Draw += this.PluginUi.Draw;
            this.PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            this.CommandManager.AddHandler("/slb", new CommandInfo(OnCommand) {
                HelpMessage = "Open Config Window for StarlightBreaker"
            });


        }

        private void DrawConfigUI() {
            this.PluginUi.IsVisible = true;
        }

        private void OnCommand(string command, string arguments) {
#if DEBUG
            GetProcessedString(arguments);
#endif
            DrawConfigUI();
        }

        private unsafe void Chat_OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled) {
            if (!(this.Configuration.Enable && this.Configuration.Coloring == Coloring.ChatLogOnly))
                return;
            if (sender?.TextValue == this.ClientState.LocalPlayer?.Name.TextValue) {
                var newPayload = new List<Payload>();
                foreach (var payload in message.Payloads) {
                    if (payload is TextPayload textPayload) {
                        var processedStr = GetProcessedString(textPayload.Text);
                        var newSeString = DiffString(textPayload.Text, processedStr);
                        newPayload.AddRange(newSeString.Payloads);
                    }
                    else
                        newPayload.Add(payload);
                }
                message.Payloads.Clear();
                message.Payloads.AddRange(newPayload);
            }
        }

        private unsafe string GetProcessedString(string str) {
            var utf8String = FFXIVClientStructs.FFXIV.Client.System.String.Utf8String.FromString(str);
            FilterSeStringDetour(this.VulgarInstance, *utf8String);
            return (*utf8String).ToString();
        }

        private unsafe void FilterSeStringDetour(IntPtr vulgarInstance, FFXIVClientStructs.FFXIV.Client.System.String.Utf8String utf8String) {
            if (vulgarInstance == IntPtr.Zero) {
                PluginLog.Error($"VulgarInstance is Zero Point!");
                return;
            }

            if (Configuration.Enable)
                return;
            PluginLog.Debug($"{utf8String.StringLength}");
            var originString = utf8String.ToString();
            PluginLog.Debug($"Before:{originString}");
            FilterSeStringHook!.Original(vulgarInstance, utf8String);
            PluginLog.Debug($"After:{utf8String}");
        }

        private bool VulgarCheckDetour(IntPtr vulgarInstance, UTF8String utf8String) {
            //Party Finder Check
            if (Configuration.Enable) {
                return false;
            }
            else {
                if (vulgarInstance == IntPtr.Zero) {
                    PluginLog.Error($"VulgarInstance is Zero Point!");
                    return false;
                }
                return VulgarCheckHook!.Original(vulgarInstance, utf8String);
            }
        }

        private SeString DiffString(string str1, string str2) {
            var seString = new SeStringBuilder();
            var i = 0;
            var length = Math.Min(str1.Length, str2.Length);
            while (i < length) {
                if (str1[i] != str2[i]) {
                    var next = i;
                    while (next < str1.Length && str1[next] != str2[next]) {
                        next++;
                    }
                    seString.AddUiForeground((ushort)this.Configuration.Color);
                    if (this.Configuration.Italics) seString.AddItalicsOn();
                    seString.AddText(str1.Substring(i, next - i));
                    if (this.Configuration.Italics) seString.AddItalicsOff();
                    seString.AddUiForegroundOff();
                    i = next;
                }
                else {
                    var next = i;
                    while (next < str1.Length && str1[next] == str2[next]) {
                        next++;
                    }
                    seString.AddText(str1.Substring(i, next - i));
                    i = next;
                }
            }
            return seString.Build();
        }

        public void Dispose() {
            this.FilterSeStringHook?.Dispose();
            this.VulgarCheckHook?.Dispose();
            this.PluginInterface.UiBuilder.Draw -= this.PluginUi.Draw;
            this.ChatGui.ChatMessage -= Chat_OnChatMessage;
            this.CommandManager.RemoveHandler("/slb");
            // this.PluginInterface.Dispose();
        }
    }

    public enum Coloring {
        None,
        ChatLogOnly,
        //All
    }
}
