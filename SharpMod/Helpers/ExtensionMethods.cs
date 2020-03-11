using System;
using System.IO;

namespace SharpMod.Helpers {
    public static class ExtensionMethods {
        private static readonly byte[] tmp2 = new byte[2];
        private static readonly byte[] tmp4 = new byte[4];

        public static UInt16 ReadUInt16(this FileStream fs) {
            fs.Read(tmp2, 0, 2);
            return BitConverter.ToUInt16(tmp2, 0);
        }

        public static UInt16 ReadUInt32(this FileStream fs) {
            fs.Read(tmp4, 0, 4);
            return BitConverter.ToUInt16(tmp4, 0);
        }
    }
}