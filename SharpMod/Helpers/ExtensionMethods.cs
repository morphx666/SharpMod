using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SharpMod.Helpers {
    public static class ExtensionMethods {
        public static UInt16 ReadUint16(this FileStream fs) {
            byte[] tmp = new byte[2];
            fs.Read(tmp, 0, 2);
            return BitConverter.ToUInt16(tmp, 0);
        }

        public static UInt16 ReadUint32(this FileStream fs) {
            byte[] tmp = new byte[4];
            fs.Read(tmp, 0, 4);
            return BitConverter.ToUInt16(tmp, 0);
        }
    }
}
