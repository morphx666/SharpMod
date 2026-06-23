using Eto.Drawing;
using System;
using System.Runtime.InteropServices;

namespace SharpModPlayerGUI {
    internal static class Renderer {
        // Reusable scratch buffers; resized only when the source dimensions change.
        private static PointF[] waveformBufL = [];
        private static PointF[] waveformBufR = [];
        private static PointF[] instrumentBuf = [];

        public static void RenderWaveform(SharpMod.SoundFile sf, byte[] buffer, Graphics g, Pen colorL, Pen colorR, RectangleF r) {
            float hh = r.Height / 2.0f;
            float hh2 = hh / 2.0f;

            int ds = sf.Is16Bit ? 2 : 1;
            int ss = (sf.IsStereo ? 2 : 1) * ds;
            int bl = buffer.Length / ss;
            if(bl < 2) return;
            float xf = r.Width / bl;
            float left = r.Left;

            if(waveformBufL.Length != bl) {
                waveformBufL = new PointF[bl];
                waveformBufR = new PointF[bl];
            }
            PointF[] pL = waveformBufL;
            PointF[] pR = waveformBufR;

            if(sf.IsStereo) {
                if(sf.Is16Bit) {
                    ReadOnlySpan<short> s = MemoryMarshal.Cast<byte, short>(buffer);
                    float topY = hh - hh2;
                    float botY = hh + hh2;
                    for(int i = 0, j = 0; i < bl; i++, j += 2) {
                        float x = left + i * xf;
                        pL[i] = new PointF(x, topY - (s[j] / 32768.0f) * hh2);
                        pR[i] = new PointF(x, botY - (s[j + 1] / 32768.0f) * hh2);
                    }
                } else {
                    for(int i = 0, j = 0; i < bl; i++, j += 2) {
                        float x = left + i * xf;
                        pL[i] = new PointF(x, ((buffer[j] + 0x80) / 256.0f) * hh2);
                        pR[i] = new PointF(x, hh + ((buffer[j + 1] + 0x80) / 256.0f) * hh2);
                    }
                }
            } else {
                if(sf.Is16Bit) {
                    ReadOnlySpan<short> s = MemoryMarshal.Cast<byte, short>(buffer);
                    for(int i = 0; i < bl; i++) {
                        float x = left + i * xf;
                        pL[i] = new PointF(x, hh - (s[i] / 32768.0f) * hh);
                    }
                } else {
                    for(int i = 0; i < bl; i++) {
                        float x = left + i * xf;
                        pL[i] = new PointF(x, ((buffer[i] + 0x80) / 256.0f) * hh);
                    }
                }
            }

            g.DrawLines(colorL, pL);
            if(sf.IsStereo) {
                g.DrawLines(colorR, pR);
                g.DrawLine(Pens.Gray, r.Left, hh, r.Right, hh);
            }
        }

        // Renders only the static sample waveform into the provided bitmap-backed Graphics.
        // The live playback cursor lines are drawn separately on top of the blitted bitmap.
        public static void RenderInstrumentSample(SharpMod.SoundFile.ModInstrument instrument, Graphics g, Pen color, int width, int height, int resolution = 32) {
            if(instrument.Sample == null) return;
            int bl = instrument.Sample.Length / resolution;
            if(bl < 2) return;
            if(instrumentBuf.Length < bl) instrumentBuf = new PointF[bl];
            PointF[] pL = instrumentBuf;
            float xf = (float)width / bl;
            byte[] sample = instrument.Sample;
            for(int i = 0; i < bl; i++) {
                float x = i * xf;
                pL[i] = new PointF(x, height - (byte)(sample[i * resolution] + 0x80) / 256.0f * height);
            }
            g.DrawLines(color, new ReadOnlySpan<PointF>(pL, 0, bl).ToArray());
        }

        public static void RenderInstrumentCursors(SharpMod.SoundFile sf, int instrumentIndex, Graphics g, RectangleF r) {
            SharpMod.SoundFile.ModChannel[] channels = sf.Channels;
            for(int c = 0; c < channels.Length - 1; c++) {
                SharpMod.SoundFile.ModChannel channel = channels[c];
                if((channel.Length != 0) && (channel.InstrumentIndex == instrumentIndex)) {
                    float x = r.Left + (float)channel.Pos / channel.Length * r.Width;
                    g.DrawLine(Pens.LightGrey, x, r.Top, x, r.Bottom);
                }
            }
        }
    }
}
