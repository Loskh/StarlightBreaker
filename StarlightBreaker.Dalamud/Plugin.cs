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
using FFXIVClientStructs.FFXIV.Client.System.Memory;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using InteropGenerator.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static FFXIVClientStructs.FFXIV.Component.GUI.AtkComponentInputBase;
using static System.Net.Mime.MediaTypeNames;
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

        public delegate bool AgentLookingForGroupTextFilterDelegate(AgentLookingForGroup* agent, Utf8String* text);
        Hook<AgentLookingForGroupTextFilterDelegate> AgentLookingForGroupTextFilterHook;

        public delegate Utf8String* RaptureTextModuleChatLogFilterDelegate(RaptureTextModule* textModule, Utf8String* text, nint unk, uint bytesNum);
        Hook<RaptureTextModuleChatLogFilterDelegate> RaptureTextModuleChatLogFilterHook;


        public delegate void RaptureTextModulePartyFinderFilterDelegate(RaptureTextModule* textModule, Utf8String* text, nint unk, bool unk1);

        public AsmHook AgentLookingForGroupDetailedWindowTextFilterHook;
        private static RaptureTextModulePartyFinderFilterDelegate RaptureTextModulePartyFinderFilterDetour =null!;
        private RaptureTextModulePartyFinderFilterDelegate RaptureTextModulePartyFinderFilterOrigin;

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
            this.AgentLookingForGroupTextFilterHook.Enable();

            this.RaptureTextModuleChatLogFilterHook = GameInteropProvider.HookFromSignature<RaptureTextModuleChatLogFilterDelegate>("40 53 48 83 EC 20 48 8D 99 ?? ?? ?? ?? 48 8B CB E8 ?? ?? ?? ?? 48 8B 0D", this.RaptureTextModuleChatLogFilterDetour);
            this.RaptureTextModuleChatLogFilterHook.Enable();

            RaptureTextModulePartyFinderFilterDetour = this.RaptureTextModulePartyFinderFilter;
            RaptureTextModulePartyFinderFilterOrigin = Marshal.GetDelegateForFunctionPointer<RaptureTextModulePartyFinderFilterDelegate>(Scanner.ScanText("E8 ?? ?? ?? ?? 44 38 A3 ?? ?? ?? ?? 74 ?? 48 8D 15"));
            var callbackPtr = Marshal.GetFunctionPointerForDelegate(RaptureTextModulePartyFinderFilterDetour);
            var caveAllocation = (nint)NativeMemory.Alloc(8 * 3);
            this.AgentLookingForGroupDetailedWindowTextFilterHook = new AsmHook(
                Scanner.ScanAllText("E8 ?? ?? ?? ?? 44 38 A3 ?? ?? ?? ?? 74 ?? 48 8D 15").First(),
                [
                    "use64",
                    $"mov r9, 0x{caveAllocation:x8}",
                    "mov [r9], rcx",
                    "mov [r9+0x8], rdx",
                    "mov [r9+0x10], r8",

                    $"mov rax, 0x{callbackPtr:x8}",
                    "call rax",

                    $"mov r9, 0x{caveAllocation:x8}",
                    "mov rcx, [r9]",
                    "mov rdx, [r9+0x8]",
                    "mov r8, [r9+0x10]",
                    "mov r9, 0",
                ],
                "AgentLookingForGroupDetailedWindowTextFilterHook",
                AsmHookBehaviour.DoNotExecuteOriginal
            );
            this.AgentLookingForGroupDetailedWindowTextFilterHook?.Enable();

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
                    }
                }
            }
            else
            {
                RaptureTextModulePartyFinderFilterOrigin(textModule, text, unk, unk1);
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

        private unsafe bool AgentLookingForGroupTextFilterDetour(AgentLookingForGroup* agent, UTF8String* text)
        {
            if (this.Configuration.PartyFinderConfig.Enable)
            {
                if (this.Configuration.PartyFinderConfig.EnableColor)
                {
                    if (this.Configuration.FontConfig.Italics || this.Configuration.FontConfig.EnableColor)
                    {
                        var original = IMemorySpace.GetDefaultSpace()->Create<Utf8String>();
                        original->Copy(text);
                        var ret = this.AgentLookingForGroupTextFilterHook.Original(agent, text);
                        if (text->EqualTo(original))
                            return ret;
                        var result = HighlightCensoredParts(original->ToString(), text->ToString(), this.Configuration.FontConfig.Italics, this.Configuration.FontConfig.EnableColor, this.Configuration.FontConfig.Color);
                        var bytes = result.Encode();
                        fixed (byte* pointer = bytes)
                        {
                            text->SetString((CStringPointer)pointer);
                        }
                        original->Dtor(true);
                    }
                }
                return true;
            }
            return this.AgentLookingForGroupTextFilterHook.Original(agent, text);
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
            this.AgentLookingForGroupDetailedWindowTextFilterHook?.Dispose();
            WindowSystem.RemoveAllWindows();
            ConfigWindow.Dispose();
            CommandManager.RemoveHandler(CommandName);
        }
    }
}
