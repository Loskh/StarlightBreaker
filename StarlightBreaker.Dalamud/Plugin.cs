using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.Text;
using InteropGenerator.Runtime;
using System.Diagnostics;
using System.Linq;
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
        internal static ISigScanner Scanner { get; private set; } = null!;
        [PluginService]
        internal static ICommandManager CommandManager { get; private set; } = null!;
        [PluginService]
        internal static IClientState ClientState { get; private set; } = null!;
        [PluginService]
        internal static IChatGui ChatGui { get; private set; } = null!;
        [PluginService]
        internal static IDataManager DataManager { get; private set; } = null!;

        [PluginService]
        internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
        [PluginService]
        internal static IPlayerState PlayerState { get; private set; } = null!;

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

        public delegate void AgentLookingForGroupTextFilterDelegate(AgentLookingForGroup* agent, Utf8String* text);
        Hook<AgentLookingForGroupTextFilterDelegate> AgentLookingForGroupTextFilterHook;

        public delegate Utf8String* RaptureTextModuleChatLogFilterDelegate(RaptureTextModule* textModule, Utf8String* text, nint unk, uint bytesNum);
        Hook<RaptureTextModuleChatLogFilterDelegate> RaptureTextModuleChatLogFilterHook;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate void RaptureTextModulePartyFinderFilterDelegate(RaptureTextModule* textModule, Utf8String* text, nint unk, bool unk1);
        private RaptureTextModulePartyFinderFilterDelegate RaptureTextModulePartyFinderFilterOrigin;
        private CallHook<RaptureTextModulePartyFinderFilterDelegate> AgentLookingForGroupDetailedWindowTextFilterHook;

        public unsafe Plugin()
        {

            this.Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            ConfigWindow = new ConfigWindow(this);
            WindowSystem.AddWindow(ConfigWindow);

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Config Window for StarlightBreaker"
            });


            this.AgentLookingForGroupTextFilterHook = GameInteropProvider.HookFromSignature<AgentLookingForGroupTextFilterDelegate>("48 89 5C 24 ?? 57 48 83 EC 20 C6 81 ?? ?? ?? ?? ?? 48 8B D9 48 8B 49 10 48 8B FA", this.AgentLookingForGroupTextFilterDetour);
            // 暂时关闭
            this.AgentLookingForGroupTextFilterHook.Enable();

            this.RaptureTextModuleChatLogFilterHook = GameInteropProvider.HookFromSignature<RaptureTextModuleChatLogFilterDelegate>("40 53 48 83 EC 20 48 8D 99 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8B 0D", this.RaptureTextModuleChatLogFilterDetour);
            this.RaptureTextModuleChatLogFilterHook.Enable();

            var hookAddress = Scanner.ScanAllText("E8 ?? ?? ?? ?? 44 38 A3 ?? ?? ?? ?? 74 ?? 48 8D 15").First();
            PluginLog.Debug($"AgentLookingForGroupDetailedWindowTextFilterHook:{Util.DescribeAddress(hookAddress)}");
            this.AgentLookingForGroupDetailedWindowTextFilterHook = new CallHook<RaptureTextModulePartyFinderFilterDelegate>(hookAddress, RaptureTextModulePartyFinderFilter);
            this.AgentLookingForGroupDetailedWindowTextFilterHook?.Enable();


            //由于"E8 ?? ?? ?? ?? 44 38 A3 ?? ?? ?? ?? 74 11"和"E8 ?? ?? ?? ?? 44 38 A3 ?? ?? ?? ?? 74 ?? 48 8D 15"是同一地址的不同签名ffxiv_dx11.exe+0x5419A3 函数地址为ffxiv_dx11.exe+0x97B820
            //之前的版本已经被Hook，因此开头不再是E9或E8,因此后面的ScanText会返回ffxiv_dx11.exe+0x5419A3,而不是0x97B820
            //var raptureTextModulePartyFinderFilterAddress = Scanner.ScanText("E8 ?? ?? ?? ?? 44 38 A3 ?? ?? ?? ?? 74 11");
            var raptureTextModulePartyFinderFilterAddress = Scanner.ScanText("40 53 56 57 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 48 8B F9 49 8B F0");
            PluginLog.Debug($"RaptureTextModulePartyFinderFilter:{Util.DescribeAddress(raptureTextModulePartyFinderFilterAddress)}");
            this.RaptureTextModulePartyFinderFilterOrigin = Marshal.GetDelegateForFunctionPointer<RaptureTextModulePartyFinderFilterDelegate>(raptureTextModulePartyFinderFilterAddress);
            //TODO:
            //E8 ?? ?? ?? ?? 0F B6 BB ?? ?? ?? ?? 40 84 FF 74 ?? 48 8D 15
            //当出现无法处理招募的时候检查文本并显示导致无法发送的地方

            if (this.Configuration.Version == 0)
            {
                this.ConfigWindow.IsOpen = true;
                this.ConfigWindow.ShowUpdateTips = true;
                this.Configuration.Version = 1;
                this.Configuration.Save();
            }
        }

        private void RaptureTextModulePartyFinderFilter(RaptureTextModule* textModule, UTF8String* text, nint unk, bool unk1)
        {
            if (this.Configuration.PartyFinderConfig.Enable)
            {
                if (this.Configuration.PartyFinderConfig.EnableColor)
                {
                    if (this.Configuration.FontConfig.Italics || this.Configuration.FontConfig.EnableColor)
                    {
                        var original = IMemorySpace.GetDefaultSpace()->Create<Utf8String>();
                        original->Ctor();
                        original->Copy(text);
                        RaptureTextModulePartyFinderFilterOrigin(textModule, text, unk, unk1);
                        if (text->EqualTo(original))
                            return;
                        var result = HighlightCensoredParts(original->ToString(), text->ToString(), this.Configuration.FontConfig.Italics, this.Configuration.FontConfig.EnableColor, this.Configuration.FontConfig.Color);
                        var bytes = result.Encode();
                        fixed (byte* pointer = bytes)
                        {
                            text->SetString((CStringPointer)pointer);
                        }
                        original->Dtor(true);
                        return;
                    }
                }
                return;
            }
            else
            {
                RaptureTextModulePartyFinderFilterOrigin(textModule, text, unk, unk1);
                return;
            }
        }


        private UTF8String* RaptureTextModuleChatLogFilterDetour(RaptureTextModule* textModule, UTF8String* text, nint unk, uint bytesNum)
        {
            if (this.Configuration.ChatLogConfig.Enable)
            {
                if (this.Configuration.ChatLogConfig.EnableColor)
                {
                    var processedString = this.RaptureTextModuleChatLogFilterHook.Original(textModule, text, unk, bytesNum);
                    if (processedString->EqualTo(text))
                        return processedString;
                    var result = HighlightCensoredParts(text->ToString(), processedString->ToString(), this.Configuration.FontConfig.Italics, this.Configuration.FontConfig.EnableColor, this.Configuration.FontConfig.Color);
                    var bytes = result.Encode();
                    fixed (byte* pointer = bytes)
                    {
                        processedString->SetString((CStringPointer)pointer);
                    }
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

        private void AgentLookingForGroupTextFilterDetour(AgentLookingForGroup* agent, UTF8String* text)
        {
            if (this.Configuration.PartyFinderConfig.Enable)
            {
                if (this.Configuration.PartyFinderConfig.EnableColor)
                {
                    if (this.Configuration.FontConfig.Italics || this.Configuration.FontConfig.EnableColor)
                    {
                        var original = IMemorySpace.GetDefaultSpace()->Create<Utf8String>();
                        original->Ctor();
                        original->Copy(text);
                        this.AgentLookingForGroupTextFilterHook.Original(agent, text);
                        if (text->EqualTo(original))
                            return;
                        var result = HighlightCensoredParts(original->ToString(), text->ToString(), this.Configuration.FontConfig.Italics, this.Configuration.FontConfig.EnableColor, this.Configuration.FontConfig.Color);
                        var bytes = result.Encode();
                        fixed (byte* pointer = bytes)
                        {
                            text->SetString((CStringPointer)pointer);
                        }
                        original->Dtor(true);
                    }
                }
                return;
            }
            else
            {
                this.AgentLookingForGroupTextFilterHook.Original(agent, text);
                return;
            }
        }

        private void DrawUI() => WindowSystem.Draw();
        public void ToggleConfigUI() => ConfigWindow.Toggle();

        private void OnCommand(string command, string arguments)
        {
            ToggleConfigUI();
        }


        private SeString HighlightCensoredParts(string original, string processed, bool italic, bool enableColor, ushort color)
        {
            int length = original.Length;
            if (length == 0)
                return SeString.Empty;

            var builder = new SeStringBuilder();
            int i = 0;

            while (i < length)
            {
                if (original[i] == processed[i])
                {
                    int start = i;
                    while (i < length && original[i] == processed[i])
                        i++;
                    builder.AddText(original.Substring(start, i - start));
                }
                else
                {
                    int start = i;
                    while (i < length && original[i] != processed[i])
                        i++;
                    if (enableColor) builder.AddUiForeground(color);
                    if (italic) builder.AddItalicsOn();
                    builder.AddText(original.Substring(start, i - start));
                    if (italic) builder.AddItalicsOff();
                    if (enableColor) builder.AddUiForegroundOff();
                }
            }

            return builder.Build();
        }

        public void Dispose()
        {
            this.AgentLookingForGroupTextFilterHook?.Dispose();
            this.RaptureTextModuleChatLogFilterHook?.Dispose();
            AgentLookingForGroupDetailedWindowTextFilterHook?.Disable();
            this.AgentLookingForGroupDetailedWindowTextFilterHook?.Dispose();
            WindowSystem.RemoveAllWindows();
            ConfigWindow.Dispose();
            CommandManager.RemoveHandler(CommandName);
        }
    }
}
