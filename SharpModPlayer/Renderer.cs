using System;
using System.Drawing;

namespace SharpModPlayer {
    public class Renderer {
        public static void RenderOutput(SharpMod.SoundFile sf, byte[] buffer, Graphics g, Pen colorL, Pen colorR, Rectangle r) {
            float hh = r.Height / 2.0f;
            float hh2 = hh / 2.0f;

            int ds = sf.Is16Bit ? 2 : 1;
            int ss = (sf.IsStereo ? 2 : 1) * ds;
            int bl = buffer.Length / ss;
            float xf = (float)r.Width / bl;

            PointF[] pL = new PointF[bl];
            PointF[] pR = new PointF[bl];

            byte[] tmpB = new byte[ds + (ds % 2)];
            float x;

            for(int i = 0, j = 0; i < bl; i++, j += ss) {
                x = r.Left + i * xf;
                if(sf.IsStereo) {
                    if(sf.Is16Bit) {
                        Array.Copy(buffer, j, tmpB, 0, ds);
                        pL[i] = new PointF(x, hh - hh2 - (BitConverter.ToInt16(tmpB, 0) / 32768.0f) * hh2);

                        Array.Copy(buffer, j + ds, tmpB, 0, ds);
                        pR[i] = new PointF(x, hh + hh2 - (BitConverter.ToInt16(tmpB, 0) / 32768.0f) * hh2);
                    } else {
                        pL[i] = new PointF(x, ((buffer[j] + 0x80) / 256.0f) * hh2);
                        pR[i] = new PointF(x, hh + ((buffer[j + 1] + 0x80) / 256.0f) * hh2);
                    }
                } else {
                    if(sf.Is16Bit) {
                        Array.Copy(buffer, j, tmpB, 0, ds);
                        pL[i] = new PointF(x, hh - (BitConverter.ToInt16(tmpB, 0) / 32768.0f) * hh);
                    } else {
                        pL[i] = new PointF(x, ((buffer[j] + 0x80) / 256.0f) * hh);
                    }
                }
            }

            g.DrawCurve(colorL, pL);
            if(sf.IsStereo) {
                g.DrawCurve(colorR, pR);
                g.DrawLine(Pens.Gray, r.Left, hh, r.Right, hh);
            }
        }

        public static void RenderInstrument(SharpMod.SoundFile sf, int instrumentIndex, Graphics g, Pen color, Rectangle r, int resolution = 32) {
            SharpMod.SoundFile.ModInstrument instrument = sf.Instruments[instrumentIndex];
            if(instrument.Sample != null) {
                float x;
                int bl = instrument.Sample.Length / resolution;
                float xf = (float)r.Width / bl;
                PointF[] pL = new PointF[bl];

                // Render Instrument's sample
                for(int i = 0; i < bl; i += 1) {
                    x = r.Left + i * xf;
                    pL[i] = new PointF(x, r.Y - (byte)(instrument.Sample[i * resolution] + 0x80) / 256.0f * r.Height);
                }
                g.DrawCurve(color, pL);

                // Render Position
                r.Y -= r.Height;
                for(int c = 0; c < sf.Channels.Length - 1; c++) {
                    SharpMod.SoundFile.ModChannel channel = sf.Channels[c];
                    if((channel.Length != 0) && (channel.InstrumentIndex == instrumentIndex)) {
                        x = r.Left + (float)channel.Pos / channel.Length * r.Width;
                        g.DrawLine(Pens.White, x, r.Y, x, r.Bottom);
                    }
                }
            }
        }
    }
}
