using NAudio.Wave;
using SharpMod;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SharpModPlayer {
    public partial class FormMain : Form {
        private readonly WaveOut waveOut;
        private CustomBufferProvider audioProvider;
        private readonly SoundFile sf;
        private readonly Pen oWfPen = new Pen(Color.Green);
        private readonly Pen cWfPen = new Pen(Color.FromArgb(128, Color.OrangeRed));
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
            //return @"D:\Users\Xavier Flix\Dropbox\Projects\SharpModPlayer\Release\mods\Spike Mix.mod";
            FileInfo[] files = (new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods"))).GetFiles("*.mod");
            return files[(new Random()).Next(files.Length)].FullName;
        }


        private void RenderWaveForm(object sender, PaintEventArgs e) {
            lock(currentBuffer) {
                Graphics g = e.Graphics;
                Rectangle r = this.DisplayRectangle;

                r.X = 400;
                r.Width -= r.X;
                Renderer.RenderOutput(sf, currentBuffer, g, oWfPen, r);

                // This is VERY inefficient!
                // Since the sample doesn't change, we should "cache it" and then simply paste the bitmap, instead of re-drawing it every time.
                // Even better, when the surface is invalidated, we could just simply invalidate the output waveform area.
                r = new Rectangle(200, (int)(this.FontHeight * 1.5), 200, this.FontHeight + 2);
                for(int i = 1; i < sf.Instruments.Length; i++) {
                    g.DrawString(sf.Instruments[i].Name, this.Font, Brushes.White, 0, r.Y - r.Height);
                    if(sf.Instruments[i].Sample != null) {
                        Renderer.RenderInstrument(sf, i, g, cWfPen, r);
                    }
                    g.DrawLine(Pens.DimGray, 0, r.Y + 1, r.Right, r.Y + 1);
                    r.Y += (r.Height + 4);
                }

                g.DrawLine(Pens.LightGray, 400, 0, 400, this.DisplayRectangle.Bottom);
            }
        }
    }
}