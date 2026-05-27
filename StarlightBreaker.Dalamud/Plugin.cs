using System;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Text;
using InteropGenerator.Runtime;
using System.Runtime.InteropServices;
using UTF8String = FFXIVClientStructs.FFXIV.Client.System.String.Utf8String;

namespace StarlightBreaker
{
    public unsafe class Plugin : IDalamudPlugin
    {
        public const string Name = "StarlightBreaker";
        private const string CommandName = "/slb";

        [PluginService]
        internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService]
        internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService]
        internal static IDataManager DataManager { get; private set; } = null!;

        [PluginService]
        internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;

        [PluginService]
        internal static IPluginLog PluginLog { get; private set; } = null!;

        public readonly WindowSystem WindowSystem = new(Name);
        private ConfigWindow ConfigWindow { get; set; }
        internal Configuration Configuration { get; set; }

        //private readonly IntPtr VulgarInstance = IntPtr.Zero;
        //private readonly IntPtr VulgarPartyInstance = IntPtr.Zero;


        //public delegate void FilterSeStringDelegate(IntPtr vulgarInstance, ref Utf8String utf8String);
        //private Hook<FilterSeStringDelegate> FilterSeStringHook;

        //public delegate bool VulgarCheckDelegate(IntPtr vulgarInstance, Utf8String utf8String);
        //private Hook<VulgarCheckDelegate> VulgarCheckHook;
        //private VulgarCheckDelegate VulgarCheck;

        public delegate Utf8String* RaptureTextModuleChatLogFilterDelegate(RaptureTextModule* textModule, Utf8String* text, nint unk, uint bytesNum);
        [Signature("40 53 48 83 EC 20 48 8D 99 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8B 0D", DetourName = nameof(RaptureTextModuleChatLogFilterDetour))]
        private Hook<RaptureTextModuleChatLogFilterDelegate> RaptureTextModuleChatLogFilterHook = null!;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate void RaptureTextModulePartyFinderFilterDelegate(RaptureTextModule* textModule, Utf8String* text, nint unk, bool unk1);
        [Signature("40 53 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B F9 49 8B F0", DetourName = nameof(RaptureTextModulePartyFinderFilterDetour), Fallibility = Fallibility.Fallible)]
        private Hook<RaptureTextModulePartyFinderFilterDelegate>? RaptureTextModulePartyFinderFilterHook = null;

        public unsafe Plugin()
        {

            this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            ConfigWindow = new ConfigWindow(this);
            WindowSystem.AddWindow(ConfigWindow);

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;
            PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUI;

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "打开 StarlightBreaker 设置窗口 / Open the StarlightBreaker config window"
            });

            GameInteropProvider.InitializeFromAttributes(this);

            this.RaptureTextModuleChatLogFilterHook.Enable();

            this.RaptureTextModulePartyFinderFilterHook?.Enable();


            //由于"E8 ?? ?? ?? ?? 44 38 A3 ?? ?? ?? ?? 74 11"和"E8 ?? ?? ?? ?? 44 38 A3 ?? ?? ?? ?? 74 ?? 48 8D 15"是同一地址的不同签名ffxiv_dx11.exe+0x5419A3 函数地址为ffxiv_dx11.exe+0x97B820
            //之前的版本已经被Hook，因此开头不再是E9或E8,因此后面的ScanText会返回ffxiv_dx11.exe+0x5419A3,而不是0x97B820
            //var raptureTextModulePartyFinderFilterAddress = Scanner.ScanText("E8 ?? ?? ?? ?? 44 38 A3 ?? ?? ?? ?? 74 11");
            //TODO:
            //E8 ?? ?? ?? ?? 0F B6 BB ?? ?? ?? ?? 40 84 FF 74 ?? 48 8D 15
            //当出现无法处理招募的时候检查文本并显示导致无法发送的地方

            if (this.Configuration.Version != Configuration.CurrentVersion)
            {
                this.ConfigWindow.IsOpen = true;
                this.ConfigWindow.ShowUpdateTips = true;
                this.Configuration.PartyFinderConfig.Enable = false;
                this.Configuration.PartyFinderConfig.EnableColor = false;
                this.Configuration.Version = Configuration.CurrentVersion;
                this.Configuration.Save();
            }
        }

        private void RaptureTextModulePartyFinderFilterDetour(RaptureTextModule* textModule, UTF8String* text, nint unk, bool unk1)
        {
            try
            {
                if (this.Configuration.PartyFinderConfig.Enable)
                {
                    if (this.Configuration.PartyFinderConfig.EnableColor)
                    {
                        if (this.Configuration.FontConfig.Italics || this.Configuration.FontConfig.EnableColor)
                        {
                            var original = IMemorySpace.GetDefaultSpace()->Create<Utf8String>();
                            original->Copy(text);
                            this.RaptureTextModulePartyFinderFilterHook!.Original(textModule, text, unk, unk1);
                            HighlightCensoredParts(original, text, this.Configuration.FontConfig.Italics, this.Configuration.FontConfig.EnableColor, this.Configuration.FontConfig.Color);
                            original->Dtor();
                            return;
                        }
                    }

                    return;
                }

                this.RaptureTextModulePartyFinderFilterHook!.Original(textModule, text, unk, unk1);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "招募板文本过滤 Hook 失败，回退到原始调用。 / Party Finder filter hook failed; falling back to original.");
                this.RaptureTextModulePartyFinderFilterHook!.Original(textModule, text, unk, unk1);
            }
        }


        private UTF8String* RaptureTextModuleChatLogFilterDetour(RaptureTextModule* textModule, UTF8String* text, nint unk, uint bytesNum)
        {
            try
            {
                if (this.Configuration.ChatLogConfig.Enable)
                {
                    if (this.Configuration.ChatLogConfig.EnableColor)
                    {
                        var processedString = this.RaptureTextModuleChatLogFilterHook.Original(textModule, text, unk, bytesNum);
                        HighlightCensoredParts(text, processedString, this.Configuration.FontConfig.Italics, this.Configuration.FontConfig.EnableColor, this.Configuration.FontConfig.Color);
                        return processedString;
                    }
                    else
                    {
                        return text;
                    }
                }
                else
                {
                    return this.RaptureTextModuleChatLogFilterHook.Original(textModule, text, unk, bytesNum);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "聊天日志过滤 Hook 失败，回退到原始调用。 / Chat log filter hook failed; falling back to original.");
                return this.RaptureTextModuleChatLogFilterHook.Original(textModule, text, unk, bytesNum);
            }
        }

        private void DrawUI() => WindowSystem.Draw();
        public void ToggleConfigUI() => ConfigWindow.Toggle();

        private void OnCommand(string command, string arguments)
        {
            ToggleConfigUI();
        }

        private void HighlightCensoredParts(Utf8String* original, Utf8String* processed, bool italic, bool enableColor, ushort color)
        {
            if (original->EqualTo(processed))
                return;

            var originalSeString = SeString.Parse(original->AsSpan());
            var processedSeString = SeString.Parse(processed->AsSpan());

            var origPayloads = originalSeString.Payloads;
            var procPayloads = processedSeString.Payloads;

            if (origPayloads.Count != procPayloads.Count)
            {
                PluginLog.Warning("Payload 数量不匹配，已跳过高亮处理。 / Payload count mismatch in HighlightCensoredParts.");
                return;
            }

            var builder = new SeStringBuilder();

            for (int i = 0; i < origPayloads.Count; i++)
            {
                var orig = origPayloads[i];
                var proc = procPayloads[i];

                if (orig.Type != PayloadType.RawText || proc.Type != PayloadType.RawText)
                {
                    builder.Add(orig);
                    continue;
                }

                var origText = (TextPayload)orig;
                var procText = (TextPayload)proc;
                var origStr = origText.Text;
                var procStr = procText.Text;

                if (origStr is null || procStr is null)
                {
                    builder.Add(origText);
                    continue;
                }

                if (origStr == procStr)
                {
                    builder.Add(origText);
                    continue;
                }

                if (origStr.Length != procStr.Length)
                {
                    builder.Add(origText);
                    continue;
                }

                int length = origStr.Length;
                int j = 0;

                while (j < length)
                {
                    if (origStr[j] == procStr[j])
                    {
                        int start = j;
                        while (j < length && origStr[j] == procStr[j])
                            j++;
                        builder.AddText(origStr.Substring(start, j - start));
                    }
                    else
                    {
                        int start = j;
                        while (j < length && origStr[j] != procStr[j])
                            j++;
                        string censoredSegment = origStr.Substring(start, j - start);

                        if (enableColor)
                            builder.AddUiForeground(color);
                        if (italic)
                            builder.AddItalicsOn();

                        builder.AddText(censoredSegment);

                        if (italic)
                            builder.AddItalicsOff();
                        if (enableColor)
                            builder.AddUiForegroundOff();
                    }
                }
            }

            processed->SetString(builder.Build().EncodeWithNullTerminator());
        }

        public void Dispose()
        {
            this.RaptureTextModuleChatLogFilterHook?.Dispose();
            this.RaptureTextModulePartyFinderFilterHook?.Dispose();
            WindowSystem.RemoveAllWindows();
            ConfigWindow.Dispose();
            CommandManager.RemoveHandler(CommandName);
        }
    }
}
