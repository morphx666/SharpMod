using System;
using System.Collections.Generic;
using System.Text;

namespace SharpMod {
    public partial class SoundFile {
        public string CommandToString(int pattern, int row, int channel) {
            string r = "";
            int inc = Type == 2 ? 4 : 6;
            int pIndex = (int)(row * ActiveChannels * inc) + channel * inc;
            byte[] cmd = new byte[inc];
            Array.Copy(patterns[pattern], pIndex, cmd, 0, inc);
            if(Type == 2) {
                byte A0 = cmd[0], A1 = cmd[1], A2 = cmd[2], A3 = cmd[3];
                int period = ((A0 & 0x0F) << 8) | (A1);
                int inst = (A2 >> 4) | (A0 & 0x10);
                int efx = A2 & 0x0F;
                int prm = A3;

                if(period != 0) {
                    int note = FrequencyToNote(((Instruments[inst].FineTune << MOD_PRECISION) * MOD_AMIGAC2) / (period * Rate)) + 6;
                    r = $"{NoteToString(note)} {inst:00}";
                } else r += "... ..";

                r += $" v64 ";
                if(efx != 0 && prm != 0) {
                    r += $"{(char)(efx | 0x40)}{prm:X2}";
                } else r += "...";

            } else {
                int mode = cmd[0];
                int note = cmd[1];
                int inst = cmd[2];
                int vol = cmd[3];
                int efx = cmd[4];
                int prm = cmd[5];

                if((mode & 0x20) != 0) {
                    r = $"{NoteToString(note)} {inst:00}";
                } else r += "... ..";

                r += " ";
                if((mode & 0x40) != 0) {
                    r += $"v{vol:00}";
                } else {
                    if(inst < Instruments.Length)
                        r += $"v{Instruments[inst].Volume:00}";
                    else
                        r += $" ..";
                };

                r += " ";
                if((mode & 0x80) != 0) {
                    r += $"{(char)(efx | 0x40)}{prm:X2}";
                } else r += "...";
            }
            return r == "" ? "... .. .. ..." : r;
        }

        private string NoteToString(int note) {
            char ss = "-#-#--#-#-#-"[note % 12];
            char ns = "CCDDEFFGGAAB"[note % 12];
            int no = note / 12;
            return $"{ns}{ss}{no}";
        }

        private int FrequencyToNote(double f) {
            return (int)((12.0 * (Math.Log(f / (440.0 / 2.0)) / Math.Log(2.0))) + 57.0);
        }
    }
}
