using OpenTK.Audio;
using OpenTK.Audio.OpenAL;
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
        private int alSrc;
        private SoundFile sndFile;

        private readonly Pen oWfPenL = new Pen(Color.FromArgb(0, 115, 170));
        private readonly Pen oWfPenR = new Pen(Color.FromArgb(0, 115, 170));
        private readonly Pen cWfPen = new Pen(Color.FromArgb(128, Color.Orange));

        private readonly SolidBrush[][] bkColor = {new SolidBrush[]{new SolidBrush(Color.FromArgb(48, 48, 48)), new SolidBrush(Color.FromArgb(98, 98, 98)) }, // active
                                                   new SolidBrush[]{new SolidBrush(Color.FromArgb(42, 42, 42)), new SolidBrush(Color.FromArgb(42, 42, 42)) }  // inactive
                                                  };

        private readonly SolidBrush[] cColor = {new SolidBrush(Color.DimGray), // inactive
                                                new SolidBrush(Color.DarkCyan), // note
                                                new SolidBrush(Color.DarkKhaki), // instrument
                                                new SolidBrush(Color.DarkGreen), // volume
                                                new SolidBrush(Color.DarkOrange), // effect
                                               };

        private readonly Font monoFont = new Font("Consolas", 15, GraphicsUnit.Pixel);
        private Size monoFontSize;
        private readonly StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center };

        private bool userHasDroppedFile = false;
        private readonly int maxChannels;
        private readonly int channelWidth;

        private byte[] buffer = Array.Empty<byte>();
        private const int sampleRate = 44100;
        private const int bitDepth = 16; // 8 | 15
        private const int channels = 2;  // 1 | 2

        private Rectangle progressRect;

        private bool isLeftMouseButtonDown;

        private bool isMono = IsRunningOnMono();

        private int fps = 30;

        public FormMain() {
            InitializeComponent();

            base.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            monoFontSize = new Size((int)(monoFont.Size - 4), monoFont.Height);
            maxChannels = 4;
            channelWidth = monoFontSize.Width * 13 + 8;

            //sndFile = new SoundFile(@"\\media-center\c\Users\xavie\Music\MODS\new mods\temp\RIAD_INS.S3M", sampleRate, bitDepth == 16, channels == 2, false);
            SetSoundFile(new SoundFile(GetRandomFile(), sampleRate, bitDepth == 16, channels == 2, false));
            UpdateTitleBarText();

            //string tmp = sndFile.CommandToString(1, 0, 0);

            this.SizeChanged += (object s, EventArgs e) => UpdateTitleBarText();
            this.Paint += new PaintEventHandler(RenderUI);

            SetupDragDropSupport();
            InitUIHandling();
            StartAudio();

            Task.Run(async () => {
                while(true) {
                    await Task.Delay(fps);
                    this.Invoke((MethodInvoker)delegate { this.Invalidate(); });
                }
            });
        }

        private void SetSoundFile(SoundFile soundFile) {
            sndFile = soundFile;
            fps = (int)Math.Max(30, Math.Floor(5000.0 / sndFile.MusicTempo));
        }

        private void InitUIHandling() {
            this.MouseDown += (_, e) => {
                isLeftMouseButtonDown = (e.Button == MouseButtons.Left);
            };

            this.MouseUp += (_, e) => {
                if(isLeftMouseButtonDown) {
                    if(IsInsideProgressBar(e)) SetPositionFromMouse(e.X);
                    isLeftMouseButtonDown = false;
                }
            };

            this.MouseMove += (_, e) => {
                Cursor c = Cursor;
                if(IsInsideProgressBar(e)) {
                    if(isLeftMouseButtonDown) SetPositionFromMouse(e.X);
                    c = Cursors.IBeam;
                } else {
                    c = Cursors.Default;
                }
                if(Cursor != c) Cursor = c;
            };
        }

        private bool IsInsideProgressBar(MouseEventArgs e) {
            return e.X >= progressRect.Left && e.X <= progressRect.Right &&
                   e.Y >= progressRect.Top && e.Y <= progressRect.Bottom;
        }

        private void SetPositionFromMouse(int x) {
            double p = (double)(x - progressRect.Left) / progressRect.Width;
            sndFile.Position = (uint)(p * sndFile.PositionCount);
        }

        private void SetupDragDropSupport() {
            this.DragOver += (object sender, DragEventArgs e) => e.Effect = DropFileIsValid(e) ? DragDropEffects.Copy : DragDropEffects.None;

            this.DragDrop += (object sender, DragEventArgs e) => {
                if(DropFileIsValid(e)) {
                    lock(buffer) {
                        userHasDroppedFile = true;
                        string[] files = (string[])(e.Data.GetData("FileDrop"));

                        try {
                            SetSoundFile(new SoundFile(files[0], sampleRate, bitDepth == 16, channels == 2, false));
                        } catch { };

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

        private void StartAudio() {
            AudioContext audioContext = new AudioContext(AudioContext.DefaultDevice, sampleRate, 0);

            int bufLen = 6000;
            int bufLen2 = bufLen / 2;
            buffer = new byte[bufLen];

            ALFormat alf = bitDepth == 16 ?
                            (channels == 2 ? ALFormat.Stereo16 : ALFormat.Mono16) :
                            (channels == 2 ? ALFormat.Stereo8 : ALFormat.Mono8);

            alSrc = AL.GenSource();

            // All this crap is just to prevent having a conditional (if)
            // inside the while loop, which is only executed once:
            //  if(AL.GetSourceState(alSrc) != ALSourceState.Playing) AL.SourcePlay(alSrc);
            //  https://github.com/morphx666/SharpMod/blob/4c46ce08023391139b074ce08e1b58c661a42199/SharpModPlayer/FormMain.cs#L182
            int buf = AL.GenBuffer();
            AL.BufferData(buf, alf, buffer, bufLen, sampleRate);
            AL.SourceQueueBuffer(alSrc, buf);
            AL.SourcePlay(alSrc);
            AL.SourceUnqueueBuffer(buf);
            AL.DeleteBuffer(buf);

            Task.Run(() => {
                int n;
                int frame = 0;
                int bpos;
                bool bufferIsClear = false;

                while(true) {
                    if(sndFile != null) {
                        n = (int)sndFile.Read(buffer, (uint)bufLen);

                        if(n == 0) {
                            if(!bufferIsClear) {
                                Array.Clear(buffer, 0, bufLen);
                                bufferIsClear = true;
                            }
                        } else if(bufferIsClear) bufferIsClear = false;
                    }

                    buf = AL.GenBuffer();

                    AL.BufferData(buf, alf, buffer, bufLen, sampleRate);
                    AL.SourceQueueBuffer(alSrc, buf);

                    do {
                        Thread.Sleep(5);
                        AL.GetSource(alSrc, ALGetSourcei.ByteOffset, out bpos);
                    } while(bpos + bufLen2 < bufLen * frame);
                    frame++;

                    AL.SourceUnqueueBuffer(buf);
                    AL.DeleteBuffer(buf);
                }
            });
        }

        private void UpdateTitleBarText() {
            if(sndFile != null) {
                uint s = sndFile.Length;
                uint m = s / 60;
                s %= 60;
                this.Text = $"SharpMod: '{sndFile.Title}' | {sndFile.Type} | {m:00}m {s:00}s";

                HScrollBarChannels.Anchor = AnchorStyles.None;
                HScrollBarChannels.Value = 0;
                HScrollBarChannels.Minimum = 0;
                HScrollBarChannels.Maximum = Math.Max(maxChannels, (int)sndFile.ActiveChannels - maxChannels);
                HScrollBarChannels.Width = channelWidth * maxChannels + 8;
                HScrollBarChannels.Left = this.DisplayRectangle.Width - HScrollBarChannels.Width - 6 + 8;
                HScrollBarChannels.Top = this.DisplayRectangle.Height - HScrollBarChannels.Height;
                HScrollBarChannels.Visible = (sndFile.ActiveChannels > maxChannels);
                HScrollBarChannels.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                HScrollBarChannels.Refresh();
            } else {
                this.Text = $"SharpMod";
                HScrollBarChannels.Visible = false;
            }
        }

        private string GetRandomFile() {
            FileInfo[] files = (new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods"))).GetFiles("*.*");
            return files[(new Random()).Next(files.Length)].FullName;
        }

        private void RenderUI(object sender, PaintEventArgs e) {
            // The following code is just a proof of concept and a huge mess at the same time...

            if(sndFile == null) return;
            lock(buffer) {
                Graphics g = e.Graphics;
                try {
                    RenderProgress(g, RenderWaveform(g, this.DisplayRectangle));
                    RenderSamples(g);
                    RenderPatterns(g);
                } catch { }; // Yep, Bad things happen sometimes... and I don't care
            }
        }

        private void RenderPatterns(Graphics g) {
            Rectangle r = new Rectangle(0, 0, channelWidth * maxChannels, this.DisplayRectangle.Height - monoFontSize.Height - (HScrollBarChannels.Visible ? HScrollBarChannels.Height : 0));
            int n = r.Height / (monoFontSize.Height * 64);
            r.Y = (int)((r.Height - monoFontSize.Height) / 2.0);
            int fromChannel = HScrollBarChannels.Value;
            int sfPptrIdx = (int)sndFile.Pattern;
            int sfRow = (int)sndFile.Row - 0; // Adjust to properly sync audio with display
            if(sfPptrIdx == 0xFF) {
                sfPptrIdx = sndFile.Order.Where((o) => o != 0xFF).Last();
                sfRow = 63;
            }

            if(sndFile.CurrentPattern > 0) RenderPattern(g, ref r, fromChannel, sndFile.Order[sndFile.CurrentPattern - 1], 64, r.Y - sfRow * monoFontSize.Height, false);
            RenderPattern(g, ref r, fromChannel, sfPptrIdx, sfRow, r.Y, true);
            if(sndFile.NextPattern != 0xFF) RenderPattern(g, ref r, fromChannel, sndFile.Order[sndFile.NextPattern], 0, r.Y - (sfRow - 64) * monoFontSize.Height, false);

            r.X = this.DisplayRectangle.Width - r.Width;
            g.FillRectangle(Brushes.LightGray, r.X - 6, 0, r.Width + 6, monoFontSize.Height);
            for(int chn = 0; chn < maxChannels; chn++) {
                r.X = this.DisplayRectangle.Width - r.Width + chn * channelWidth;
                g.DrawString($"Channel {chn + fromChannel + 1}", monoFont, Brushes.DarkSlateBlue, r.X + (channelWidth - monoFontSize.Width * 8) / 2, 0);
                g.DrawLine(Pens.DimGray, r.X - 6, 0, r.X - 6, r.Bottom);
            }
        }

        private void RenderSamples(Graphics g) {
            const int mn = 22;
            // FIXME: This is VERY inefficient!
            // Since the sample doesn't change, we should "cache it" and then simply paste the bitmap, instead of re-drawing it every time.
            // Even better, when the surface is invalidated, we could just simply invalidate the output waveform area.
            Rectangle r = new Rectangle(200, (int)(this.FontHeight * 1.5), 200, this.FontHeight + 2);
            for(int i = 1; i < sndFile.Instruments.Length; i++) {
                string n = sndFile.Instruments[i].Name;
                if(n.Length > mn) n = n.Substring(0, mn - 1) + "…";
                g.DrawString(n, monoFont, Brushes.White, 0, r.Y - r.Height);
                if(sndFile.Instruments[i].Sample != null) {
                    Renderer.RenderInstrument(sndFile, i, g, cWfPen, r);
                }
                g.DrawLine(Pens.DimGray, 0, r.Y + 1, r.Right, r.Y + 1);
                r.Y += (r.Height + 4);
            }
            g.DrawLine(Pens.DimGray, r.X - 4, 0, r.X - 4, this.DisplayRectangle.Bottom);
            g.DrawLine(Pens.DimGray, 400, 0, 400, this.DisplayRectangle.Bottom);
        }

        private void RenderProgress(Graphics g, Rectangle r) {
            r.Y = this.DisplayRectangle.Height - 20;
            r.Height = 20;
            progressRect = r;
            g.FillRectangle(Brushes.DimGray, r);
            r.Width = (int)(r.Width * (double)sndFile.Position / sndFile.PositionCount);
            g.FillRectangle(oWfPenL.Brush, r);
        }

        private Rectangle RenderWaveform(Graphics g, Rectangle r) {
            r.X = 400;
            r.Width -= (int)(r.X + channelWidth * maxChannels + 6);
            r.Height -= 20;
            if(!userHasDroppedFile) g.DrawString("Drop a new MOD file\nto start playing it", this.Font, Brushes.Gray, r, sf);
            Renderer.RenderWaveform(sndFile, buffer, g, oWfPenL, oWfPenR, r);
            return r;
        }

        private void RenderPattern(Graphics g, ref Rectangle r, int fromChannel, int sfPptrIdx, int sfRow, int y, bool active) {
            string cCmd;
            int yo;
            for(int row = 0; row < 64; row++) {
                yo = y - (sfRow - row) * monoFontSize.Height;
                if(yo < 0) continue;
                if(yo >= r.Height) break;

                for(int chn = 0; chn < maxChannels; chn++) {
                    cCmd = sndFile.CommandToString(sfPptrIdx, row, chn + fromChannel);

                    r.X = this.DisplayRectangle.Width - r.Width + chn * channelWidth;
                    if((row == sfRow) || (row % 4) == 0) {
                        g.FillRectangle(bkColor[active ? 0 : 1][((row == sfRow) || (row % 4) == 0) ? ((row == sfRow) ? 1 : 0) : 0], r.X, yo, r.Width, monoFontSize.Height);
                    }
                    string[] cmds = cCmd.Split(' ');
                    int xo = 0;
                    for(int i = 0; i < cmds.Length; i++) {
                        g.DrawString(cmds[i],
                                        monoFont,
                                        cColor[active ? i + 1 : 0],
                                        r.X + xo, yo);
                        xo += (cmds[i].Length + 1) * (monoFontSize.Width - 1);
                    }

                    g.DrawLine(Pens.DimGray, r.X - 6, 0, r.X - 6, r.Bottom);
                }
            }
        }

        private static bool IsRunningOnMono() {
            return Type.GetType("Mono.Runtime") != null;
        }
    }
}