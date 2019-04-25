using NAudio.Wave;
using SharpMod;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharpModPlayer {
    public partial class FormMain : Form {
        private readonly WaveOut waveOut;
        private CustomBufferProvider audioProvider;
        private readonly SoundFile sf;
        private readonly Pen wfPen = new Pen(Color.Green);
        private byte[] currentBuffer = new byte[0];
        private const int sampleRate = 44100;
        private const int bitDepth = 16; // 8 | 15
        private const int channels = 2; // 1 | 2

        public FormMain() {
            InitializeComponent();

            base.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

            sf = new SoundFile(GetRandomFile(), sampleRate, bitDepth == 16, channels == 2, false);

            string str = "";
            uint s = sf.Length;
            uint m = s / 60;
            s %= 60;
            this.Text = $"SharpMod: '{sf.Title}' [{m:00}:{s:00}]";
            for(int i = 1; i < 32; i++) {
                str += $"{sf.Instruments[i].Name} {Environment.NewLine}";
            }
            LabelInfo.Text = str;

            waveOut = new WaveOut() {
                NumberOfBuffers = 32,
                DesiredLatency = 300
            };

            audioProvider = new CustomBufferProvider(new CustomBufferProvider.FillBuffer(FillAudioBuffer), sampleRate, bitDepth, channels);
            waveOut.Init(audioProvider);
            waveOut.Volume = 1.0f;
            waveOut.Play();

            this.Paint += new PaintEventHandler(RenderWaveForm);
            Task.Run(() => {
                while(true) {
                    Thread.Sleep(60);
                    this.Invalidate();
                }
            });
        }

        private int FillAudioBuffer(byte[] buffer) {
            int n = (int)sf.Read(buffer, (uint)buffer.Length);
            currentBuffer = (byte[])buffer.Clone();
            return n;
        }

        private string GetRandomFile() {
            FileInfo[] files = (new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods"))).GetFiles("*.mod");
            //return files[1].FullName;
            return files[(new Random()).Next(files.Length)].FullName;
        }


        private void RenderWaveForm(object sender, PaintEventArgs e) {
            lock(this.currentBuffer) {
                int w = this.DisplayRectangle.Width;
                int h = this.DisplayRectangle.Height;

                float hh = (float)(h / 2);
                float hh2 = hh / 2f;

                int ds = bitDepth / 8;
                int ss = channels * ds;
                int bl = currentBuffer.Length / ss;

                PointF[] pL = new PointF[bl];
                PointF[] pR = new PointF[bl];

                byte[] tmpB = new byte[ds + (ds % 2)];
                float x;

                for(int i = 0, j = 0; i < bl; i++, j += ss) {
                    x = (float)i * w / bl;
                    switch(channels) {
                        case 1:
                            switch(bitDepth) {
                                case 8:
                                    pL[i] = new PointF(x, hh - currentBuffer[j] + 0x80);
                                    break;
                                case 16:
                                    Array.Copy(currentBuffer, j, tmpB, 0, ds);
                                    pL[i] = new PointF(x, hh - BitConverter.ToInt16(tmpB, 0) / 256);
                                    break;
                            }
                            break;
                        case 2:
                            switch(bitDepth) {
                                case 8:
                                    pL[i] = new PointF(x, hh - hh2 - currentBuffer[j] + 0x80);
                                    pR[i] = new PointF(x, hh + hh2 - currentBuffer[j + 1] + 0x80);
                                    break;
                                case 16:
                                    Array.Copy(currentBuffer, j, tmpB, 0, ds);
                                    pL[i] = new PointF(x, hh - hh2 - BitConverter.ToInt16(tmpB, 0) / 256);

                                    Array.Copy(currentBuffer, j + ds, tmpB, 0, ds);
                                    pR[i] = new PointF(x, hh + hh2 - BitConverter.ToInt16(tmpB, 0) / 256);
                                    break;
                            }
                            break;
                    }
                }
                e.Graphics.DrawCurve(wfPen, pL);
                if(channels == 2) e.Graphics.DrawCurve(wfPen, pR);
            }
        }
    }
}
