using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.String;
using InteropGenerator.Runtime;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UTF8String = FFXIVClientStructs.FFXIV.Client.System.String.Utf8String;

namespace StarlightBreaker {
    public class Plugin : IDalamudPlugin {
        public const string Name ="StarlightBreaker";
        private const string CommandName = "/slb";

        [PluginService]
        internal static IDalamudPluginInterface PluginInterface { get; set; }
        [PluginService]
        internal static ISigScanner Scanner { get; set; }
        [PluginService]
        internal static ICommandManager CommandManager { get; set; }
        [PluginService]
        internal static IClientState ClientState { get; set; }
        [PluginService]
        internal static IChatGui ChatGui { get; set; }
        [PluginService]
        internal static IDataManager DataManager { get; set; }
        [PluginService]
        internal static IFramework Framework { get; set; }

        [PluginService]
        internal static IGameInteropProvider GameInteropProvider { get; set; }

        [PluginService]
        internal static IPluginLog PluginLog { get; set; }

        public readonly WindowSystem WindowSystem = new(Name);
        private ConfigWindow ConfigWindow { get; set; }
        internal Configuration Configuration { get; set; }

        private readonly IntPtr VulgarInstance = IntPtr.Zero;
        private readonly IntPtr VulgarPartyInstance = IntPtr.Zero;


        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate void FilterSeStringDelegate(IntPtr vulgarInstance, ref Utf8String utf8String);
        private Hook<FilterSeStringDelegate> FilterSeStringHook;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate bool VulgarCheckDelegate(IntPtr vulgarInstance, Utf8String utf8String);
        private Hook<VulgarCheckDelegate> VulgarCheckHook;

        public Plugin() {

            this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            ConfigWindow = new ConfigWindow(this);
            WindowSystem.AddWindow(ConfigWindow);

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Config Window for StarlightBreaker"
            });

            try {
                // 48 8B 0D ?? ?? ?? ?? 48 8B 81 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B D3
                var frameworkPtr = Marshal.ReadIntPtr(Scanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8B 81 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B D3"));
                VulgarInstance = Marshal.ReadIntPtr( frameworkPtr + 0x2B48);
                VulgarPartyInstance = VulgarInstance + 0x8;
#if DEBUG
                //PluginLog.Debug($"{frameworkPtr - Process.GetCurrentProcess().MainModule.BaseAddress:X}");
                PluginLog.Debug($"VulgarInstance:{VulgarInstance:X}");
#endif
                if (Scanner.TryScanText("E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 ?? 5B C3 ?? ?? ?? ?? ?? ?? ?? 48 83 EC ?? 48 8B CA", out var ptr0)) {
                    this.FilterSeStringHook = GameInteropProvider.HookFromAddress<FilterSeStringDelegate>(ptr0, this.FilterSeStringDetour);
                }
                PluginLog.Debug($"FilterSeString:{ptr0:X}");
                this.FilterSeStringHook?.Enable();

                if (Scanner.TryScanText("E8 ?? ?? ?? ?? 84 C0 74 16 48 8D 15 ?? ?? ?? ??", out var ptr1)) {
                    this.VulgarCheckHook = GameInteropProvider.HookFromAddress<VulgarCheckDelegate>(ptr1, this.VulgarCheckDetour);
                }
                PluginLog.Debug($"VulgarCheck:{ptr1:X}");
                this.VulgarCheckHook?.Enable();

                ChatGui.ChatMessage += Chat_OnChatMessage;
            }
            catch (Exception ex) {
                PluginLog.Error(ex, "开启屏蔽词染色失败", Array.Empty<object>());
            }

        }
        private void DrawUI() => WindowSystem.Draw();
        public void ToggleConfigUI() => ConfigWindow.Toggle();

        private void OnCommand(string command, string arguments) {
#if DEBUG
            PluginLog.Info(GetProcessedString(arguments));
#endif
            ToggleConfigUI();
        }

        private unsafe void Chat_OnChatMessage(XivChatType type, int timestamp, ref SeString sender, ref SeString message, ref bool isHandled) {
            if (!this.Configuration.Enable|| this.Configuration.Coloring is Coloring.None or Coloring.All) return;
            if ((sender?.TextValue).IsNullOrWhitespace()) return;
            if (this.Configuration.Coloring == Coloring.ChatLogOnlyMyself && sender?.TextValue != ClientState.LocalPlayer?.Name.TextValue) return;
            if (!IsFilterChatType(type)) return;
            var newPayload = new List<Payload>();
            foreach (var payload in message.Payloads)
            {
                if (payload is TextPayload textPayload)
                {
                    var processedStr = GetProcessedString(textPayload.Text);
                    var newSeString = DiffString(textPayload.Text, processedStr);
                    newPayload.AddRange(newSeString.Payloads);
                    PluginLog.Debug($"{textPayload.Text} -> {processedStr}");
                }
                else
                    newPayload.Add(payload);
            }
            message.Payloads.Clear();
            message.Payloads.AddRange(newPayload);
        }

        private bool IsFilterChatType(XivChatType type)
        {
            var typeValue = (ushort)type;
            return typeValue 
                is >= 10 and <= 24 
                or 27 or 30 or 32 or 36 or 37
                or >=101 and <=107;
        }

        private unsafe string GetProcessedString(string str)
        {
            Utf8String utf8String = *Utf8String.FromString(str);
            FilterSeStringHook!.OriginalDisposeSafe(this.VulgarInstance, ref utf8String);
            return utf8String.ToString();
        }

        private unsafe void FilterSeStringDetour(IntPtr vulgarInstance, ref Utf8String utf8String) {
            if (vulgarInstance == IntPtr.Zero) {
                PluginLog.Error($"VulgarInstance is Zero Point!");
                return;
            }

            if (Configuration.Enable)
            {
                if (Configuration.Coloring != Coloring.All) return;
                var originalString = utf8String.ToString();
                var processedString = GetProcessedString(originalString);
                var result = DiffString(originalString, processedString);
                var bytes = result.Encode();
                fixed (byte* pointer = bytes)
                {
                    utf8String.SetString((CStringPointer)pointer);
                }
                return;
            }
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

            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();

            CommandManager.RemoveHandler(CommandName);
            // this.PluginInterface.Dispose();
        }
    }

    public enum Coloring {
        None,
        ChatLogOnly,
        ChatLogOnlyMyself, 
        All
    }
}
