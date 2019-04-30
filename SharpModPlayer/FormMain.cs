using NAudio.Wave;
using SharpMod;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

// App icon provided by Icons8: https://icons8.com/

namespace SharpModPlayer {
    public partial class FormMain : Form {
        private WaveOut waveOut;
        private CustomBufferProvider audioProvider;
        private SoundFile sndFile;

        private readonly Pen oWfPenL = new Pen(Color.FromArgb(0, 115, 170));
        private readonly Pen oWfPenR = new Pen(Color.FromArgb(0, 115, 170)); // new Pen(Color.FromArgb(0, 255, 255));
        private readonly Pen cWfPen = new Pen(Color.FromArgb(128, Color.Orange));
        private bool userHasDroppedFile = false;
        private StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center };

        private byte[] currentBuffer = new byte[0];
        private const int sampleRate = 44100;
        private const int bitDepth = 16; // 8 | 15
        private const int channels = 2; // 1 | 2

        public FormMain() {
            InitializeComponent();

            base.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

            sndFile = new SoundFile(@"mods\MOVEIT.S3M", sampleRate, bitDepth == 16, channels == 2, false);
            //sndFile = new SoundFile(GetRandomFile(), sampleRate, bitDepth == 16, channels == 2, false);
            UpdateTitleBarText();

            this.Paint += new PaintEventHandler(RenderWaveForms);
            Task.Run(() => {
                while(true) {
                    Thread.Sleep(60);
                    this.Invalidate();
                }
            });

            SetupDragDropSupport();
            InitAudio();
        }

        private void SetupDragDropSupport() {
            this.DragOver += (object sender, DragEventArgs e) => e.Effect = DropFileIsValid(e) ? DragDropEffects.Copy : DragDropEffects.None;

            this.DragDrop += (object sender, DragEventArgs e) => {
                if(DropFileIsValid(e)) {
                    lock(currentBuffer) {
                        userHasDroppedFile = true;
                        string[] files = (string[])(e.Data.GetData("FileDrop"));

                        waveOut.Stop();
                        try {
                            sndFile = new SoundFile(files[0], sampleRate, bitDepth == 16, channels == 2, false);
                        } catch { };
                        waveOut.Play();

                        this.Invoke((MethodInvoker)delegate { UpdateTitleBarText(); });
                    }
                }
            };
        }

        private bool DropFileIsValid(DragEventArgs e) {
            bool isValid = false;
            if(e.Data.GetFormats().Contains("FileDrop")) {
                string[] files = (string[])(e.Data.GetData("FileDrop"));
                if(files.Length == 1) {
                    SoundFile tmpSf = null;
                    try {
                        tmpSf = new SoundFile(files[0], sampleRate, bitDepth == 16, channels == 2, false);
                    } catch { };
                    if(tmpSf != null) isValid = (bool)tmpSf?.IsValid;
                }
            }
            return isValid;
        }

        private void InitAudio() {
            waveOut = new WaveOut() {
                NumberOfBuffers = 16,
                DesiredLatency = 300
            };

            audioProvider = new CustomBufferProvider(new CustomBufferProvider.FillBuffer(FillAudioBuffer), sampleRate, bitDepth, channels);
            waveOut.Init(audioProvider);
            waveOut.Volume = 1.0f;
            waveOut.Play();
        }

        private void UpdateTitleBarText() {
            if(sndFile != null) {
                uint s = sndFile.Length;
                uint m = s / 60;
                s %= 60;
                this.Text = $"SharpMod: '{sndFile.Title}' [{m:00}m {s:00}s]";
            } else {
                this.Text = $"SharpMod";
            }
        }

        private int FillAudioBuffer(byte[] buffer) {
            int n = 0;
            if(sndFile != null) {
                n = (int)sndFile.Read(buffer, (uint)buffer.Length);
                currentBuffer = (byte[])buffer.Clone();
            }
            return n;
        }

        private string GetRandomFile() {
            FileInfo[] files = (new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods"))).GetFiles("*.mod");
            return files[(new Random()).Next(files.Length)].FullName;
        }


        private void RenderWaveForms(object sender, PaintEventArgs e) {
            if(sndFile == null) return;
            lock(currentBuffer) {
                Graphics g = e.Graphics;
                Rectangle r = this.DisplayRectangle;

                r.X = 400;
                r.Width -= r.X;
                if(!userHasDroppedFile) g.DrawString("Drop a new MOD file\nto start playing it", this.Font, Brushes.Gray, r, sf);
                Renderer.RenderOutput(sndFile, currentBuffer, g, oWfPenL, oWfPenR, r);

                // FIXME: This is VERY inefficient!
                // Since the sample doesn't change, we should "cache it" and then simply paste the bitmap, instead of re-drawing it every time.
                // Even better, when the surface is invalidated, we could just simply invalidate the output waveform area.
                r = new Rectangle(200, (int)(this.FontHeight * 1.5), 200, this.FontHeight + 2);
                for(int i = 1; i < sndFile.Instruments.Length; i++) {
                    g.DrawString(sndFile.Instruments[i].Name, this.Font, Brushes.White, 0, r.Y - r.Height);
                    if(sndFile.Instruments[i].Sample != null) {
                        Renderer.RenderInstrument(sndFile, i, g, cWfPen, r);
                    }
                    g.DrawLine(Pens.DimGray, 0, r.Y + 1, r.Right, r.Y + 1);
                    r.Y += (r.Height + 4);
                }

                g.DrawLine(Pens.LightGray, 400, 0, 400, this.DisplayRectangle.Bottom);
            }
        }
    }
}