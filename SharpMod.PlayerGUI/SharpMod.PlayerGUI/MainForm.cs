using Eto.Drawing;
using Eto.Forms;
using OpenTK.Audio.OpenAL;
using SharpMod;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SharpModPlayerGUI {
    public partial class MainForm : Form {
        protected Drawable Canvas;

        private int alSrc;
        private SoundFile sndFile;

        private readonly Pen oWfPenL = new(Color.FromArgb(0, 115, 170));
        private readonly Pen oWfPenR = new(Color.FromArgb(0, 115, 170));
        private readonly Pen cWfPen = new(Color.FromArgb(0xff, 0xa5, 0x00, 128)); // orange with alpha

        private readonly SolidBrush[][] bkColor = [[new(Color.FromArgb(48, 48, 48)), new(Color.FromArgb(98, 98, 98))], // active
                                                   [new(Color.FromArgb(42, 42, 42)), new(Color.FromArgb(42, 42, 42))]  // inactive
                                                  ];

        private readonly SolidBrush[] cColor = [new(Colors.DimGray), // inactive
                                                new(Colors.DarkCyan), // note
                                                new(Colors.DarkKhaki), // instrument
                                                new(Colors.DarkGreen), // volume
                                                new(Colors.DarkOrange), // effect
                                               ];

        private Font monoFont;
        private SizeF monoFontSize;

        private bool userHasDroppedFile = false;
        private uint maxChannels;
        private float channelWidth;

        private byte[] buffer = [];
        private readonly object sync = new();
        private static readonly string[] supportedExtensions = [".mod", ".stm", ".s3m", ".xm", ".669"];
        private const int sampleRate = 44100;
        private const int bitDepth = 16; // 8 | 15
        private const int channels = 2;  // 1 | 2

        private RectangleF progressRect;

        private bool isLeftMouseButtonDown;
        private PointF mouseDownPos;
        private bool isResizing;

        private int fps = 30;
        private UITimer renderTimer;
        const int sampleCharsCount = 26;

        public MainForm() {
            Title = "SharpModPlayer";
            MinimumSize = new Size(800, 600);

            Canvas = new Drawable();
            Content = Canvas;

            this.Shown += (s, e) => {
                this.Maximize();

                FontFamily monoFontFamily = new("Consolas");
                if(monoFontFamily.LocalizedName == "Consolas") {
                    monoFont = new Font(monoFontFamily, 12);
                } else {
                    monoFont = new Font(FontFamilies.Monospace, 12);
                }

                using(Bitmap bmp = new(ClientSize, PixelFormat.Format32bppRgba)) {
                    using(Graphics g = new(bmp)) {
                        monoFontSize = g.MeasureString(monoFont, "W");
                    }
                }

                maxChannels = 4;
                channelWidth = monoFontSize.Width * 13 + 8;

                SetSoundFile(new SoundFile(GetRandomFile(), sampleRate, bitDepth == 16, channels == 2, false));
                UpdateTitleBarText();

                this.SizeChanged += (s, e) => UpdateTitleBarText();
                Canvas.Paint += RenderUI;

                SetupDragDropSupport();
                InitUIHandling();
                StartAudio();

                renderTimer = new UITimer { Interval = 1.0 / fps };
                renderTimer.Elapsed += (_, _) => Canvas.Invalidate();
                renderTimer.Start();
            };
        }

        private void SetSoundFile(SoundFile soundFile) {
            sndFile = soundFile;
            fps = (int)Math.Max(30, Math.Floor(5000.0 / sndFile.MusicTempo));
            if(renderTimer != null) renderTimer.Interval = 1.0 / fps;
        }

        private void InitUIHandling() {
            this.MouseDown += (_, e) => {
                isLeftMouseButtonDown = (e.Buttons & MouseButtons.Primary) != 0;
                mouseDownPos = e.Location;
            };

            this.MouseUp += (_, e) => {
                if(isLeftMouseButtonDown) {
                    if(IsInsideProgressBar(e)) SetPositionFromMouse(e.Location.X);
                    isLeftMouseButtonDown = false;
                }
                isResizing = false;
            };

            this.MouseMove += (_, e) => {
                Cursor c = Cursor;
                if(IsInsideProgressBar(e)) {
                    if(isLeftMouseButtonDown) SetPositionFromMouse(e.Location.X);
                    c = Cursors.IBeam;
                } else if(IsOverChannelsDiv(e) || isResizing) {
                    if(isLeftMouseButtonDown) {
                        ResizeChannels(e.Location.X);
                        isResizing = true;
                    }
                    c = Cursors.SizeLeft; // .SizeWE;
                } else {
                    c = Cursors.Default;
                }
                if(Cursor != c) Cursor = c;
            };
        }

        private bool IsInsideProgressBar(MouseEventArgs e) {
            return e.Location.X >= progressRect.Left && e.Location.X <= progressRect.Right &&
                   e.Location.Y >= progressRect.Top && e.Location.Y <= progressRect.Bottom;
        }

        private bool IsOverChannelsDiv(MouseEventArgs e) {
            return e.Location.X >= progressRect.Right - 2 && e.Location.X <= progressRect.Right + 2;
        }

        private void SetPositionFromMouse(float x) {
            float p = (x - progressRect.Left) / progressRect.Width;
            sndFile.Position = (uint)(p * sndFile.PositionCount);
        }

        private void ResizeChannels(float x) {
            const int minWaveformWidth = 200;
            float w = (mouseDownPos.X - x) / channelWidth;
            if(w != 0) {
                w = w > 0 ? 1 : -1; // Normalize
                uint newMaxChannels = (uint)(maxChannels + w);

                if((newMaxChannels <= 0) ||
                    (newMaxChannels > sndFile.ActiveChannels) ||
                    (w > 0 && DisplayRectangle().Width - 400 - newMaxChannels * channelWidth < minWaveformWidth)) {
                    return;
                }
                maxChannels = newMaxChannels;
                UpdateTitleBarText();

                mouseDownPos = new(x, mouseDownPos.Y);
            }
        }

        private void SetupDragDropSupport() {
            Canvas.AllowDrop = true;

            Canvas.DragOver += (object sender, DragEventArgs e) => e.Effects = DropFileIsValid(e) ? DragEffects.Copy : DragEffects.None;

            Canvas.DragDrop += (object sender, DragEventArgs e) => {
                if(!DropFileIsValid(e)) return;

                SoundFile sf;
                try {
                    sf = new SoundFile(e.Data.Uris[0].LocalPath, sampleRate, bitDepth == 16, channels == 2, false);
                } catch { return; }
                if(!sf.IsValid) return;

                lock(sync) {
                    userHasDroppedFile = true;
                    SetSoundFile(sf);
                    if(maxChannels > sndFile.ActiveChannels) maxChannels = sndFile.ActiveChannels;
                }

                Application.Instance.AsyncInvoke(UpdateTitleBarText);
            };
        }

        private static bool DropFileIsValid(DragEventArgs e) {
            if(!e.Data.ContainsUris) return false;
            Uri[] uris = e.Data.Uris;
            if(uris == null || uris.Length != 1 || !uris[0].IsFile) return false;
            string ext = Path.GetExtension(uris[0].LocalPath).ToLowerInvariant();
            return supportedExtensions.Contains(ext);
        }

        private void StartAudio() {
            ALDevice device = ALC.OpenDevice(null);
            ALContextAttributes attributes = new() { Frequency = sampleRate };
            ALContext context = ALC.CreateContext(device, attributes);
            ALC.MakeContextCurrent(context);

            int bufferLen = 6000;
            int bufferLen2 = bufferLen / 2;
            buffer = new byte[bufferLen];

            ALFormat alf = bitDepth == 16 ?
                            (channels == 2 ? ALFormat.Stereo16 : ALFormat.Mono16) :
                            (channels == 2 ? ALFormat.Stereo8 : ALFormat.Mono8);

            alSrc = AL.GenSource();

            // All this crap is just to prevent having a conditional (if)
            // inside the while loop, which is only executed once:
            //  if(AL.GetSourceState(alSrc) != ALSourceState.Playing) AL.SourcePlay(alSrc);
            //  https://github.com/morphx666/SharpMod/blob/4c46ce08023391139b074ce08e1b58c661a42199/SharpModPlayer/FormMain.cs#L182
            int buf = AL.GenBuffer();
            AL.BufferData(buf, alf, buffer, sampleRate);
            AL.SourceQueueBuffer(alSrc, buf);
            AL.SourcePlay(alSrc);
            AL.SourceUnqueueBuffer(buf);
            AL.DeleteBuffer(buf);

            Task.Run(async () => {
                int dataReadLength;
                int frame = 0;
                int bufferPosition;
                bool bufferIsClear = false;

                while(true) {
                    if(sndFile != null) {
                        dataReadLength = (int)sndFile.Read(buffer, (uint)bufferLen);

                        if(dataReadLength == 0) {
                            if(!bufferIsClear) {
                                Array.Clear(buffer, 0, bufferLen);
                                bufferIsClear = true;
                            }
                        } else if(bufferIsClear) bufferIsClear = false;
                    }

                    buf = AL.GenBuffer();

                    AL.BufferData(buf, alf, buffer, sampleRate);
                    AL.SourceQueueBuffer(alSrc, buf);

                    do {
                        await Task.Delay(5);
                        AL.GetSource(alSrc, ALGetSourcei.ByteOffset, out bufferPosition);
                    } while(bufferPosition + bufferLen2 < bufferLen * frame);
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
                this.Title = $"SharpMod: '{sndFile.Title}' | {sndFile.Type} | {m:00}m {s:00}s";

                //HScrollBarChannels.Anchor = AnchorStyles.None;
                //HScrollBarChannels.Value = 0;
                //HScrollBarChannels.Minimum = 0;
                //HScrollBarChannels.Maximum = (int)(sndFile.ActiveChannels - maxChannels);
                //HScrollBarChannels.Width = (int)(channelWidth * maxChannels + 8);
                //HScrollBarChannels.Left = DisplayRectangle.Width - HScrollBarChannels.Width - 6 + 8;
                //HScrollBarChannels.Top = DisplayRectangle.Height - HScrollBarChannels.Height;
                //HScrollBarChannels.Visible = (sndFile.ActiveChannels > maxChannels);
                //HScrollBarChannels.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
                //HScrollBarChannels.Refresh();
            } else {
                this.Title = $"SharpMod";
                //HScrollBarChannels.Visible = false;
            }
        }

        private string GetRandomFile() {
            FileInfo[] files = (new DirectoryInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mods"))).GetFiles("*.*");
            return files[(new Random()).Next(files.Length)].FullName;
        }

        private void RenderUI(object sender, PaintEventArgs e) {
            if(sndFile == null) return;
            lock(sync) {
                Graphics g = e.Graphics;
                g.Clear(Colors.Black);
                try {
                    RenderProgress(g, RenderWaveform(g, DisplayRectangle()));
                    RenderSamples(g);
                    RenderPatterns(g);
                } catch { }
                ; // Yep, Bad things happen sometimes... and I don't care
            }
        }

        private void RenderPatterns(Graphics g) {
            RectangleF r = new(0, 0, (int)(channelWidth * maxChannels), DisplayRectangle().Height - monoFontSize.Height - 0); ; //new(0, 0, (int)(channelWidth * maxChannels), DisplayRectangle().Height - monoFontSize.Height - (HScrollBarChannels.Visible ? HScrollBarChannels.Height : 0));
            float n = r.Height / (monoFontSize.Height * 64);
            r.Y = (int)((r.Height - monoFontSize.Height) / 2.0);
            int fromChannel = 0; // HScrollBarChannels.Value;
            uint patternIndex = sndFile.Pattern;
            int sfRow = (int)sndFile.Row - 0; // Adjust to properly sync audio with display
            if(patternIndex == 0xFF) {
                patternIndex = sndFile.Order.Last((o) => o != 0xFF);
                sfRow = 63;
            }

            if(sndFile.CurrentPattern > 0) RenderPattern(g, ref r, fromChannel, sndFile.Order[sndFile.CurrentPattern - 1], 64, r.Y - sfRow * monoFontSize.Height, false);
            RenderPattern(g, ref r, fromChannel, patternIndex, sfRow, r.Y, true);
            if(sndFile.NextPattern != 0xFF) RenderPattern(g, ref r, fromChannel, sndFile.Order[sndFile.NextPattern], 0, r.Y - (sfRow - 64) * monoFontSize.Height, false);

            r.X = DisplayRectangle().Width - r.Width;
            g.FillRectangle(Brushes.LightGrey, r.X - 6, 0, r.Width + 6, monoFontSize.Height);
            for(int chn = 0; chn < maxChannels; chn++) {
                r.X = DisplayRectangle().Width - r.Width + chn * channelWidth;
                g.DrawText(monoFont, Brushes.DarkSlateBlue, r.X + (channelWidth - monoFontSize.Width * 8) / 2, 0, $"Channel {chn + fromChannel + 1}");
                g.DrawLine(Pens.DimGray, r.X - 6, 0, r.X - 6, r.Bottom);
            }
        }

        private void RenderSamples(Graphics g) {
            // FIXME: This is VERY inefficient!
            // Since the sample doesn't change, we should "cache it" and then simply paste the bitmap, instead of re-drawing it every time.
            // Even better, when the surface is invalidated, we could just simply invalidate the output waveform area.
            RectangleF r = new(monoFontSize.Width * sampleCharsCount, (int)(monoFontSize.Height * 1.5), 200, monoFontSize.Height + 2);
            for(int i = 1; i < sndFile.Instruments.Length; i++) {
                string n = sndFile.Instruments[i].Name;
                if(n.Length >= sampleCharsCount) n = string.Concat(n.AsSpan(0, sampleCharsCount - 2), "…");
                g.DrawText(monoFont, Brushes.White, 0, r.Y - r.Height, n);
                if(sndFile.Instruments[i].Sample != null) {
                    Renderer.RenderInstrument(sndFile, i, g, cWfPen, r);
                }
                g.DrawLine(Pens.DimGray, 0, r.Y + 1, r.Right, r.Y + 1);
                r.Y += (r.Height + 4);
            }
            g.DrawLine(Pens.DimGray, r.X - 4, 0, r.X - 4, DisplayRectangle().Bottom);
            r.X = monoFontSize.Width * sampleCharsCount + 200;
            g.DrawLine(Pens.DimGray, r.X, 0, r.X, DisplayRectangle().Bottom);
        }

        private void RenderProgress(Graphics g, RectangleF r) {
            r.Y = DisplayRectangle().Height - 20;
            r.Height = 20;
            progressRect = r;
            g.FillRectangle(Brushes.DimGray, r);
            r.Width = (int)(r.Width * (double)sndFile.Position / sndFile.PositionCount);
            g.FillRectangle(oWfPenL.Brush, r);
        }

        private RectangleF RenderWaveform(Graphics g, RectangleF r) {
            r.X = monoFontSize.Width * sampleCharsCount + 200;
            r.Width -= r.X + channelWidth * maxChannels + 6;
            r.Height -= 20;
            if(!userHasDroppedFile) g.DrawText(monoFont, Brushes.Gray, r.Location, "Drop a new MOD file\nto start playing it");
            Renderer.RenderWaveform(sndFile, buffer, g, oWfPenL, oWfPenR, r);
            return r;
        }

        private void RenderPattern(Graphics g, ref RectangleF r, int fromChannel, uint patternIndex, int sfRow, float y, bool active) {
            for(int row = 0; row < 64; row++) {
                float yo = y - (sfRow - row) * monoFontSize.Height;
                if(yo < 0) continue;
                if(yo >= r.Height) break;

                for(int chn = 0; chn < maxChannels; chn++) {
                    string command = sndFile.CommandToString(patternIndex, (uint)row, chn + fromChannel);

                    r.X = DisplayRectangle().Width - r.Width + chn * channelWidth;
                    if((row == sfRow) || (row % 4) == 0) {
                        g.FillRectangle(bkColor[active ? 0 : 1][((row == sfRow) || (row % 4) == 0) ? ((row == sfRow) ? 1 : 0) : 0], r.X, yo, r.Width, monoFontSize.Height);
                    }
                    string[] cmds = command.Split(' ');
                    float xo = 0;
                    for(int i = 0; i < cmds.Length; i++) {
                        g.DrawText(monoFont,
                                        cColor[active ? i + 1 : 0],
                                        r.X + xo, yo,
                                        cmds[i]);
                        xo += (cmds[i].Length + 1) * (monoFontSize.Width - 1);
                    }

                    g.DrawLine(Pens.DimGray, r.X - 6, 0, r.X - 6, r.Bottom);
                }
            }
        }

        private RectangleF DisplayRectangle() {
            return new RectangleF(PointF.Empty, this.ClientSize);
        }
    }
}
