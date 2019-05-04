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
        private readonly Font monoFont = new Font("Consolas", 13, GraphicsUnit.Pixel);
        private Size monoFontSize;
        private readonly int maxChannels;
        private readonly int channelWidth;
        private readonly StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center };

        private byte[] currentBuffer = new byte[0];
        private const int sampleRate = 44100;
        private const int bitDepth = 16; // 8 | 15
        private const int channels = 2; // 1 | 2

        public FormMain() {
            InitializeComponent();

            base.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            monoFontSize = new Size((int)(monoFont.Size - 4), monoFont.Height);
            maxChannels = 4;
            channelWidth = monoFontSize.Width * 13;

            //sndFile = new SoundFile(@"\\Media-center\c\Users\xavie\Music\MODS\SoftMix\Party Mix '90.XM", sampleRate, bitDepth == 16, channels == 2, false);
            sndFile = new SoundFile(GetRandomFile(), sampleRate, bitDepth == 16, channels == 2, false);
            UpdateTitleBarText();

            string tmp = sndFile.CommandToString(1, 0, 0);

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
                this.Text = $"SharpMod: '{sndFile.Title}' | {sndFile.Type} | {m:00}m {s:00}s";

                hScrollBarChannels.Value = 0;
                hScrollBarChannels.Minimum = 0;
                hScrollBarChannels.Maximum = Math.Max(maxChannels, (int)sndFile.ActiveChannels - maxChannels);
                hScrollBarChannels.Width = channelWidth * maxChannels;
                hScrollBarChannels.Left = this.DisplayRectangle.Width - hScrollBarChannels.Width - 6;
                hScrollBarChannels.Top = this.DisplayRectangle.Height - hScrollBarChannels.Height;
                hScrollBarChannels.Visible = (sndFile.ActiveChannels > maxChannels);
            } else {
                this.Text = $"SharpMod";
                hScrollBarChannels.Visible = false;
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
            FileInfo[] files = (new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods"))).GetFiles("*.*");
            return files[(new Random()).Next(files.Length)].FullName;
        }

        private void RenderWaveForms(object sender, PaintEventArgs e) {
            // The following code is just a proof of concept and a huge mess as the same time...

            if(sndFile == null) return;
            lock(currentBuffer) {
                Graphics g = e.Graphics;
                Rectangle r = this.DisplayRectangle;

                // Render Output Waveform
                r.X = 400;
                r.Width -= (int)(r.X + channelWidth * maxChannels + 6);
                r.Height -= 20;
                if(!userHasDroppedFile) g.DrawString("Drop a new MOD file\nto start playing it", this.Font, Brushes.Gray, r, sf);
                Renderer.RenderOutput(sndFile, currentBuffer, g, oWfPenL, oWfPenR, r);

                // Render Progress
                r.Y = this.DisplayRectangle.Height - 20;
                r.Height = 20;
                g.FillRectangle(Brushes.DimGray, r);
                r.Width = (int)(r.Width * (double)sndFile.Position / sndFile.PositionCount);
                g.FillRectangle(oWfPenL.Brush, r);

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

                g.DrawLine(Pens.DimGray, 400, 0, 400, this.DisplayRectangle.Bottom);

                // Render Patterns
                string cCmd;
                r = new Rectangle(0, 0, channelWidth * maxChannels, this.DisplayRectangle.Height - monoFontSize.Height - (hScrollBarChannels.Visible ? hScrollBarChannels.Height : 0));
                int n = r.Height / (monoFontSize.Height * 64);
                r.Y = (int)((r.Height - monoFontSize.Height) / 2.0);
                int fromChannel = hScrollBarChannels.Value;
                int sfPptrIdx = (int)sndFile.Pattern;
                int sfRow = (int)sndFile.Row;
                if(sfPptrIdx == 0xFF) {
                    sfPptrIdx = sndFile.Order.Where((o) => o != 0xFF).Last();
                    sfRow = 63;
                }

                for(int row = 0; row < 64; row++) {
                    for(int chn = 0; chn < maxChannels; chn++) {
                        cCmd = sndFile.CommandToString(sfPptrIdx, row, chn + fromChannel);

                        r.X = this.DisplayRectangle.Width - r.Width + chn * channelWidth;
                        if((row == sfRow) || (row % 4) == 0) {
                            g.FillRectangle(row == sfRow ? Brushes.LightGray : Brushes.Black, r.X, r.Y - (sfRow - row) * monoFontSize.Height, r.Width, monoFontSize.Height);
                        }
                        g.DrawString(cCmd, monoFont, row == sfRow ? Brushes.Blue : Brushes.Silver, r.X, r.Y - (sfRow - row) * monoFontSize.Height);

                        g.DrawLine(Pens.DimGray, r.X - 6, 0, r.X - 6, r.Bottom);
                    }
                }

                r.X = this.DisplayRectangle.Width - r.Width;
                g.FillRectangle(Brushes.LightGray, r.X - 6, 0, r.Width + 6, monoFontSize.Height);
                for(int chn = 0; chn < maxChannels; chn++) {
                    r.X = this.DisplayRectangle.Width - r.Width + chn * channelWidth;
                    g.DrawString($"Channel {chn + fromChannel + 1}", monoFont, Brushes.DarkSlateBlue, r.X + (channelWidth - monoFontSize.Width * 8) / 2, 0);
                    g.DrawLine(Pens.DimGray, r.X - 6, 0, r.X - 6, r.Bottom);
                }
            }
        }
    }
}