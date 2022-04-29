using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
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
using Dalamud.Utility.Signatures;
using UTF8String = FFXIVClientStructs.FFXIV.Client.System.String.Utf8String;

namespace StarlightBreaker
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "StarlightBreaker";

        [PluginService]
        [RequiredVersion("1.0")]
        private DalamudPluginInterface PluginInterface { get; init; }
        [PluginService]
        [RequiredVersion("1.0")]
        private SigScanner Scanner { get; init; }
        [PluginService]
        [RequiredVersion("1.0")]
        private CommandManager CommandManager { get; init; }
        [PluginService]
        [RequiredVersion("1.0")]
        private ClientState ClientState { get; init; }
        [PluginService]
        [RequiredVersion("1.0")]
        private ChatGui ChatGui { get; init; }
        [PluginService]
        [RequiredVersion("1.0")]
        internal DataManager DataManager { get; init; }
        [PluginService]
        [RequiredVersion("1.0")]
        internal Framework Framework { get; init; }

        private PluginUI PluginUi { get; init; }
        internal Configuration Configuration { get; init; }

        private readonly IntPtr VulgarInstance = IntPtr.Zero;
        private readonly IntPtr VulgarPartyInstance = IntPtr.Zero;


        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate void FilterSeStringDelegate(IntPtr vulgarInstance,  FFXIVClientStructs.FFXIV.Client.System.String.Utf8String utf8String);
        //[Signature("E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 ?? 5B C3 ?? ?? ?? ?? ?? ?? ?? 48 83 EC ?? 48 8B CA", DetourName = nameof(FilterSeStringDetour))]
        private Hook<FilterSeStringDelegate> FilterSeStringHook;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate bool VulgarCheckDelegate(IntPtr vulgarInstance, FFXIVClientStructs.FFXIV.Client.System.String.Utf8String utf8String);
        //[Signature("E8 ?? ?? ?? ?? 84 C0 74 16 48 8D 15 ?? ?? ?? ??", DetourName = nameof(VulgarCheckDetour))]
        private Hook<VulgarCheckDelegate> VulgarCheckHook;

        public Plugin()
        {
            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);
            PluginLog.Debug($"{this.Configuration.Color}");
            PluginLog.Debug($"{this.Configuration.Coloring}");
            this.PluginUi = new PluginUI(this);
#if DEBUG
            DrawConfigUI();
#endif

            try {
                // 48 8B 0D ?? ?? ?? ?? 48 8B 81 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B D3
                VulgarInstance = Marshal.ReadIntPtr(Framework.Address.BaseAddress + 0x2B40);
                VulgarPartyInstance = Marshal.ReadIntPtr(Framework.Address.BaseAddress + 0x2B40 + 0x8);
                SetVulgarStatus(!Configuration.Enable);

                //SignatureHelper.Initialise(this);
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

            this.CommandManager.AddHandler("/slb", new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Config Window for StarlightBreaker"
            });


        }

        private void DrawConfigUI()
        {
            this.PluginUi.IsVisible = true;
        }

        private void OnCommand(string command, string arguments)
        {
#if DEBUG
            GetProcessedString(arguments);
#endif
            DrawConfigUI();
        }

        private unsafe void Chat_OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
            if (!(this.Configuration.Enable && this.Configuration.Coloring == Coloring.ChatLogOnly))
                return;

            var senderPayload = sender.Payloads.Where(payload => payload is TextPayload).FirstOrDefault();
            if (senderPayload != default(Payload) && senderPayload is TextPayload name) {
                if (name.Text == this.ClientState.LocalPlayer?.Name.TextValue) {
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
        }

        private unsafe string GetProcessedString(string str)
        {
            var utf8String = FFXIVClientStructs.FFXIV.Client.System.String.Utf8String.FromString(str);
            FilterSeStringDetour(this.VulgarInstance, *utf8String);
            return (*utf8String).ToString();
        }

        private unsafe void FilterSeStringDetour(IntPtr vulgarInstance, FFXIVClientStructs.FFXIV.Client.System.String.Utf8String utf8String)
        {
            if (vulgarInstance == IntPtr.Zero) {
                PluginLog.Error($"VulgarInstance is Zero Point!");
                return;
            }
            PluginLog.Debug($"{utf8String.StringLength}");
            var originString = utf8String.ToString();
            PluginLog.Debug($"Before:{originString}");
            FilterSeStringHook!.Original(vulgarInstance, utf8String);
            PluginLog.Debug($"After:{utf8String}");

            if (!(this.Configuration.Coloring == Coloring.All))
                return;
            PluginLog.Debug("ALL");
            var text = DiffString(originString, utf8String.ToString());
            PluginLog.Debug($"{text}");
            var encodedBytes = text.Encode();
            var bytes = stackalloc byte[encodedBytes.Length];
            //bytes[encodedBytes.Length] = 0;
            //Unsafe.InitBlockUnaligned(bytes, 0, (uint)(encodedBytes.Length + 1));
            Marshal.Copy(encodedBytes, 0, (IntPtr)bytes, encodedBytes.Length);
            utf8String.SetString(bytes);
            //PluginLog.Debug($"{utf8String.BufSize}");
            //PluginLog.Debug($"{utf8String.BufUsed}");
            //Marshal.FreeHGlobal((IntPtr)bp);
            utf8String.BufUsed = encodedBytes.Length;
            PluginLog.Debug($"{utf8String.BufUsed}");
            PluginLog.Debug($"{utf8String.StringLength}");
            //utf8String.BufUsed -= 1;
            Dalamud.Utility.Util.DumpMemory((IntPtr)utf8String.StringPtr, (int)utf8String.BufSize);
            //Dalamud.Utility.Util.DumpMemory((IntPtr)utf8String.StringPtr, (int)utf8String.BufSize);

        }

        private bool VulgarCheckDetour(IntPtr vulgarInstance, UTF8String utf8String)
        {
            if (this.Configuration.Coloring==Coloring.All) {
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

        private SeString DiffString(string str1, string str2)
        {
            var seString = new SeStringBuilder();
            var i = 0;
            var length = Math.Min(str1.Length, str2.Length);
            while (i < length) {
                if (str1[i] != str2[i]) {
                    var next = i;
                    while (next < str1.Length && str1[next] != str2[next]) {
                        next++;
                    }
                    //seString.AddUiForeground((ushort)this.Configuration.Color);
                    if (this.Configuration.Italics) seString.AddItalicsOn();
                    seString.AddText(str1.Substring(i, next - i));
                    if (this.Configuration.Italics) seString.AddItalicsOff();
                    //seString.AddUiForegroundOff();
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

        internal void SetVulgarStatus(bool active)
        {
            if (!active) {
                Marshal.WriteIntPtr(Framework.Address.BaseAddress + 0x2B40, IntPtr.Zero);
                Marshal.WriteIntPtr(Framework.Address.BaseAddress + 0x2B48, IntPtr.Zero);
            }
            else {
                Marshal.WriteIntPtr(Framework.Address.BaseAddress + 0x2B40, VulgarInstance);
                Marshal.WriteIntPtr(Framework.Address.BaseAddress + 0x2B48, VulgarPartyInstance);
            }

        }

        public void Dispose()
        {
            SetVulgarStatus(true);
            this.FilterSeStringHook?.Dispose();
            this.VulgarCheckHook?.Dispose();
            this.PluginInterface.UiBuilder.Draw -= this.PluginUi.Draw;
            this.ChatGui.ChatMessage -= Chat_OnChatMessage;
            this.CommandManager.RemoveHandler("/slb");
            // this.PluginInterface.Dispose();
        }
    }

    public enum Coloring
    {
        None,
        ChatLogOnly,
        All
    }
}
