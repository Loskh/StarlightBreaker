using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Dalamud.Game.Command;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;

namespace StarlightBreaker
{
    public class Plugin : IDalamudPlugin
    {
        public DalamudPluginInterface pluginInterface;
        public Configuration configuration;

        public string Name => "StarlightBreaker";

        private Patch ChatLogStarPatch;

        private Patch pfinderStarPatch;

        private Patch pfinderDialogStarPatch;

        private PluginUI ui;
        private IntPtr unk = IntPtr.Zero;

        [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
        public delegate IntPtr FilterSeStringDelegate(IntPtr unk, IntPtr seString);
        public FilterSeStringDelegate FilterSeStringFunc;

        [StructLayout(LayoutKind.Explicit)]
        private readonly struct ChatPayload : IDisposable
        {
            [FieldOffset(0)]
            private readonly IntPtr textPtr;

            [FieldOffset(16)]
            private readonly ulong textLen;

            [FieldOffset(8)]
            private readonly ulong unk1;

            [FieldOffset(24)]
            private readonly ulong unk2;

            internal ChatPayload(string text)
            {
                var stringBytes = Encoding.UTF8.GetBytes(text);
#if DEBUG
                PluginLog.Log($"stringBytes={stringBytes.Length}");
#endif
                this.textPtr = Marshal.AllocHGlobal(stringBytes.Length + 30);
                Marshal.Copy(stringBytes, 0, this.textPtr, stringBytes.Length);
                Marshal.WriteByte(this.textPtr + stringBytes.Length, 0);

                this.textLen = (ulong)(stringBytes.Length + 1);

                this.unk1 = 0x200;
                this.unk2 = 0;
            }

            public void Dispose()
            {
                Marshal.FreeHGlobal(this.textPtr);
            }
        }

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
            this.configuration = this.pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.configuration.Initialize(this.pluginInterface);
            this.ui = new PluginUI(this);
#if DEBUG
            DrawConfigUI();
#endif
            try
            {
                ChatLogStarPatch = new Patch(IntPtr.Zero, new byte[1] { 0xEB });
                pfinderStarPatch = new Patch(IntPtr.Zero, new byte[5] { 0x90, 0x90, 0x90, 0x90, 0x90 });
                pfinderDialogStarPatch = new Patch(IntPtr.Zero, new byte[5] { 0x90, 0x90, 0x90, 0x90, 0x90 });

                ChatLogStarPatch.Address = pluginInterface.TargetModuleScanner.ScanText("74 ?? 48 8B D3 E8 ?? ?? ?? ?? 48 8B C3");
                pfinderStarPatch.Address = pluginInterface.TargetModuleScanner.ScanText("48 8B D6 E8 ?? ?? ?? ?? 80 BF") + 3;
                pfinderDialogStarPatch.Address = pluginInterface.TargetModuleScanner.ScanText("4C 8B C7 E8 ?? ?? ?? ?? 40 38 B3") + 3;

                UpdataPatch();
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "开启反和谐失败\n如果已经使用ACT版本的StarlightBreaker，请关闭此ACT插件", Array.Empty<object>());
            }

            try {
                var g_Framework_2 = this.pluginInterface.TargetModuleScanner.GetStaticAddressFromSig("48 8B 0D ?? ?? ?? ?? 48 8B 81 ?? ?? ?? ?? 48 85 C0 74 ?? 48 8B D3");
                unk = Marshal.ReadIntPtr(Marshal.ReadIntPtr(g_Framework_2) + 0x29D8);

                var filterSeStringPtr = this.pluginInterface.TargetModuleScanner.ScanText("E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 ?? 5B C3 ?? ?? ?? ?? ?? ?? ?? 48 83 EC ?? 48 8B CA");
                FilterSeStringFunc = Marshal.GetDelegateForFunctionPointer<FilterSeStringDelegate>(filterSeStringPtr);

                this.pluginInterface.Framework.Gui.Chat.OnChatMessage += Chat_OnChatMessage;

            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "开启屏蔽词染色失败", Array.Empty<object>());
            }
            this.pluginInterface.UiBuilder.OnBuildUi += this.ui.Draw;
            this.pluginInterface.UiBuilder.OnOpenConfigUi += (sender, args) => DrawConfigUI();

            this.pluginInterface.CommandManager.AddHandler("/slb", new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Config Window for StarlightBreaker"
            });
        }

        public void UpdataPatch()
        {
            if (configuration.Enable)
            {
                ChatLogStarPatch.Enable();
                pfinderStarPatch.Enable();
                pfinderDialogStarPatch.Enable();
            }
            else {
                ChatLogStarPatch.Disable();
                pfinderStarPatch.Disable();
                pfinderDialogStarPatch.Disable();
            }

        }

        private void DrawConfigUI()
        {
            this.ui.IsVisible = true;
        }

        private void OnCommand(string command, string arguments)
        {
#if DEBUG
            PluginLog.Log($"origin:{arguments}");
            var testStr = GetProcessedString(arguments);
            PluginLog.Log($"origin:{testStr}");
#endif
            DrawConfigUI();
        }

        private void Chat_OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
        {
#if DEBUG
            PluginLog.Log(type.ToString());
            PluginLog.Log(sender.TextValue);
#endif
            if (!this.configuration.EnableColorWords)
                return;
            if (sender.TextValue == "")
                return;
            if (this.pluginInterface.ClientState.LocalPlayer == null)
                return;
            var senderName = GetSenderName(sender.TextValue);
            if (senderName != this.pluginInterface.ClientState.LocalPlayer.Name)
                return;
            var newPayload = new List<Payload>();
            foreach (var payload in message.Payloads)
            {
                if (payload is TextPayload textPayload)
                {
                    var processedStr = GetProcessedString(textPayload.Text);
                    if (processedStr.Length != textPayload.Text.Length)
                        continue;
                    var newSeString = DiffString(textPayload.Text, processedStr);
#if DEBUG
                    PluginLog.Log(processedStr);
#endif
                    newPayload.AddRange(newSeString);
                }
                else
                    newPayload.Add(payload);
            }
            message.Payloads.Clear();
            message.Payloads.AddRange(newPayload);
        }

        public string GetSenderName(string str)
        {
            //return Regex.Replace(str, "[^\u4E00-\u9FFFA-Za-z ·]", "");
            //remove SeIconChar
            return Regex.Replace(str, "[\uE030-\uE100]", "");
        }

        public string GetProcessedString(string str)
        {
            if (unk == IntPtr.Zero)
                return str;
            using var payload = new ChatPayload(str);
            var mem1 = Marshal.AllocHGlobal(400);
            Marshal.StructureToPtr(payload, mem1, false);
            this.FilterSeStringFunc(this.unk, mem1);
            var processedStr = GetSeStringFromPtr(Marshal.ReadIntPtr(mem1));
            Marshal.FreeHGlobal(mem1);
            return processedStr.TextValue;
        }

        internal SeString GetSeStringFromPtr(IntPtr seStringPtr)
        {
            byte b;
            var offset = 0;
            unsafe
            {
                while ((b = *(byte*)(seStringPtr + offset)) != 0)
                    offset++;
            }
            var bytes = new byte[offset];
            Marshal.Copy(seStringPtr, bytes, 0, offset);
            return pluginInterface.SeStringManager.Parse(bytes);
        }

        private List<Payload> DiffString(string str1, string str2)
        {
            var data = this.pluginInterface.Data;
            var seString = new List<Payload> { };
            var i = 0;
            while (i < str1.Length)
            {
                if (str1.Substring(i, 1) != str2.Substring(i, 1))
                {
                    var next = i;
                    while (next < str1.Length && str1.Substring(next, 1) != str2.Substring(next, 1))
                    {
                        next++;
                    }
                    seString.Add(new UIForegroundPayload(data, (ushort)configuration.Color));
                    seString.Add(new TextPayload(str1.Substring(i, next - i)));
                    seString.Add(new UIForegroundPayload(data, 0));
                    i = next;
                }
                else
                {
                    var next = i;
                    while (next < str1.Length && str1.Substring(next, 1) == str2.Substring(next, 1))
                    {
                        next++;
                    }
                    seString.Add(new TextPayload(str1.Substring(i, next - i)));
                    i = next;
                }
            }
            return seString;
        }

#region IDisposable Support
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;
            this.pluginInterface.UiBuilder.OnBuildUi -= this.ui.Draw;
            this.pluginInterface.Framework.Gui.Chat.OnChatMessage -= Chat_OnChatMessage;
            this.pluginInterface.CommandManager.RemoveHandler("/slb");
            if (ChatLogStarPatch != null)
            {
                ChatLogStarPatch.Dispose();
            }
            if (pfinderStarPatch != null)
            {
                pfinderStarPatch.Dispose();
            }
            if (pfinderDialogStarPatch != null)
            {
                pfinderDialogStarPatch.Dispose();
            }

            this.pluginInterface.Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
#endregion
    }

    public class Patch : IDisposable
    {
        public IntPtr Address;

        public byte[] OriginBytes;

        public byte[] PatchBytes;

        public bool Enabled;

        public Patch(IntPtr address, byte[] patchBytes)
        {
            if (Address == IntPtr.Zero)
                return;
            Address = address;
            OriginBytes = new byte[patchBytes.Length];
            PatchBytes = patchBytes;
            Dalamud.SafeMemory.ReadBytes(Address, OriginBytes.Length, out OriginBytes);
        }

        public void Enable()
        {
            if (Address == IntPtr.Zero)
                return;
            Dalamud.SafeMemory.WriteBytes(Address, PatchBytes);
            Enabled = true;
        }

        public void Disable()
        {
            if (Address == IntPtr.Zero)
                return;
            Dalamud.SafeMemory.WriteBytes(Address, OriginBytes);
            Enabled = false;
        }

        public void Dispose()
        {
            if (Enabled)
            {
                Disable();
            }
        }
    }
}
