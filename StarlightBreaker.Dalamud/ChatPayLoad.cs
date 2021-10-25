using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace StarlightBreaker
{
    [StructLayout(LayoutKind.Explicit)]
    public struct ChatPayload : IDisposable
    {
        [FieldOffset(0x00)]private readonly IntPtr StringPtr;
        [FieldOffset(0x8)] public long BufSize; // default buffer = 0x40
        [FieldOffset(0x10)] public long BufUsed;
        [FieldOffset(0x18)] public long StringLength; // string length not including null terminator
        [FieldOffset(0x20)] public byte IsEmpty;
        [FieldOffset(0x21)] public byte IsUsingInlineBuffer;

        internal ChatPayload(string text)
        {
            //var stringBytes = Encoding.UTF8.GetBytes(text);
#if DEBUG
                //PluginLog.Log($"stringBytes={stringBytes.Length}");
#endif
            //this.textPtr = Marshal.AllocHGlobal(stringBytes.Length + 30);
            //Marshal.Copy(stringBytes, 0, this.textPtr, stringBytes.Length);
            //Marshal.WriteByte(this.textPtr + stringBytes.Length, 0);

            this.StringPtr = Marshal.StringToCoTaskMemUTF8(text);
            this.BufUsed = text.Length * 4 + 1;

            //this.BufSize = 0x200;
            this.BufSize = BufUsed;
            this.StringLength = text.Length;
            this.IsEmpty = 0;
            this.IsUsingInlineBuffer = 0;
        }

        public void Dispose()
        {
            //Marshal.FreeHGlobal(this.textPtr);
            Marshal.FreeCoTaskMem(this.StringPtr);
        }
    }
}
