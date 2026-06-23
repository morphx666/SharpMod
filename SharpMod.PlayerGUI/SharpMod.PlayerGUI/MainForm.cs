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
        private readonly SolidBrush progressBrush = new(Color.FromArgb(0, 115, 170));

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

        // Per-instrument cached waveform bitmaps. The sample data never changes while a
        // track plays, so the expensive curve rendering happens once and is blitted on
        // every subsequent paint. Invalidated whenever the loaded SoundFile changes.
        private Bitmap[] sampleWaveformBitmaps = [];
        private int sampleBitmapW, sampleBitmapH;

        // Composite samples-panel bitmap: instrument names, waveform blits and the
        // horizontal/vertical dividers baked into a single image so each frame only
        // costs one DrawImage plus the live per-channel cursor lines.
        private Bitmap samplesPanelBitmap;
        private int samplesPanelW, samplesPanelH;

        // Pre-rendered "empty cell" placeholder ("... .. ... ...") for active and
        // inactive patterns. Wholly-empty cells are the majority of any pattern, so
        // blitting these is far cheaper than four DrawText calls per cell.
        private Bitmap emptyCellActiveBitmap;
        private Bitmap emptyCellInactiveBitmap;
        private int emptyCellW, emptyCellH;

        // Pre-tokenized pattern commands keyed by [patternIndex][row * ActiveChannels + channel].
        // Cells parse on first access and are reused across frames so RenderPattern doesn't
        // allocate per cell per frame. Shared sentinel covers the pattern == 0xFF case.
        private string[][][] cmdTokensCache = [];
        private static readonly string[] emptyTokens = ["...", "..", "...", "..."];

        // Last rendered playback markers; used by the render timer to invalidate only the
        // regions that actually changed between ticks.
        private uint lastRow = uint.MaxValue;
        private uint lastCurrentPattern = uint.MaxValue;
        private uint lastPosition = uint.MaxValue;

        private struct Layout {
            public RectangleF Samples;
            public RectangleF Waveform;
            public RectangleF Patterns;
            public RectangleF Progress;
        }

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

                this.SizeChanged += (s, e) => { UpdateTitleBarText(); InvalidateAllAreas(); };
                Canvas.Paint += RenderUI;

                SetupDragDropSupport();
                InitUIHandling();
                StartAudio();

                renderTimer = new UITimer { Interval = 1.0 / fps };
                renderTimer.Elapsed += (_, _) => InvalidateChangedAreas();
                renderTimer.Start();
            };
        }

        private void SetSoundFile(SoundFile soundFile) {
            sndFile = soundFile;
            fps = (int)Math.Max(30, Math.Floor(5000.0 / sndFile.MusicTempo));
            if(renderTimer != null) renderTimer.Interval = 1.0 / fps;
            ResetSampleBitmapCache();
            cmdTokensCache = new string[soundFile.Patterns.Length][][];
            lastRow = uint.MaxValue;
            lastCurrentPattern = uint.MaxValue;
            lastPosition = uint.MaxValue;
        }

        private void ResetSampleBitmapCache() {
            for(int i = 0; i < sampleWaveformBitmaps.Length; i++) sampleWaveformBitmaps[i]?.Dispose();
            sampleWaveformBitmaps = [];
            sampleBitmapW = 0;
            sampleBitmapH = 0;
            samplesPanelBitmap?.Dispose();
            samplesPanelBitmap = null;
            samplesPanelW = 0;
            samplesPanelH = 0;
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

        private Layout ComputeLayout() {
            Size cs = ClientSize;
            float w = cs.Width;
            float h = cs.Height;
            float samplesRight = monoFontSize.Width * sampleCharsCount + 200;
            float patternsW = channelWidth * maxChannels + 6;
            float patternsX = w - patternsW;
            float waveLeft = samplesRight + 1;
            return new Layout {
                Samples = new RectangleF(0, 0, samplesRight + 1, h),
                Waveform = new RectangleF(waveLeft, 0, Math.Max(0, patternsX - waveLeft), h - 20),
                Patterns = new RectangleF(patternsX, 0, patternsW, h),
                Progress = new RectangleF(waveLeft, h - 20, Math.Max(0, patternsX - waveLeft), 20),
            };
        }

        private static Rectangle ToInt(RectangleF r) {
            int x = (int)Math.Floor(r.X);
            int y = (int)Math.Floor(r.Y);
            return new Rectangle(x, y, (int)Math.Ceiling(r.Right) - x, (int)Math.Ceiling(r.Bottom) - y);
        }

        private void InvalidateAllAreas() {
            lastRow = uint.MaxValue;
            lastCurrentPattern = uint.MaxValue;
            lastPosition = uint.MaxValue;
            Canvas?.Invalidate();
        }

        private void InvalidateChangedAreas() {
            if(sndFile == null) return;
            Layout L = ComputeLayout();
            // Oscilloscope and per-instrument playback cursors move every frame.
            Canvas.Invalidate(ToInt(L.Waveform));
            Canvas.Invalidate(ToInt(L.Samples));
            uint pos = sndFile.Position;
            if(pos != lastPosition) {
                Canvas.Invalidate(ToInt(L.Progress));
                lastPosition = pos;
            }
            uint row = sndFile.Row;
            uint cur = sndFile.CurrentPattern;
            if(row != lastRow || cur != lastCurrentPattern) {
                Canvas.Invalidate(ToInt(L.Patterns));
                lastRow = row;
                lastCurrentPattern = cur;
            }
        }

        private void RenderUI(object sender, PaintEventArgs e) {
            if(sndFile == null) return;
            lock(sync) {
                Graphics g = e.Graphics;
                RectangleF clip = e.ClipRectangle;
                Layout L = ComputeLayout();
                try {
                    if(clip.Intersects(L.Samples)) {
                        g.FillRectangle(Brushes.Black, L.Samples);
                        RenderSamples(g, L);
                    }
                    if(clip.Intersects(L.Waveform)) {
                        g.FillRectangle(Brushes.Black, L.Waveform);
                        RenderWaveform(g, L);
                    }
                    if(clip.Intersects(L.Patterns)) {
                        g.FillRectangle(Brushes.Black, L.Patterns);
                        RenderPatterns(g, L);
                    }
                    if(clip.Intersects(L.Progress)) {
                        g.FillRectangle(Brushes.Black, L.Progress);
                        RenderProgress(g, L);
                    }
                } catch { }
                ; // Yep, Bad things happen sometimes... and I don't care
            }
        }

        private void RenderPatterns(Graphics g, Layout L) {
            RectangleF r = new(0, 0, (int)(channelWidth * maxChannels), L.Patterns.Height - monoFontSize.Height - 20);
            r.Y = (int)((r.Height - monoFontSize.Height) / 2.0);
            int fromChannel = 0;
            uint patternIndex = sndFile.Pattern;
            int sfRow = (int)sndFile.Row;
            if(patternIndex == 0xFF) {
                patternIndex = sndFile.Order.Last((o) => o != 0xFF);
                sfRow = 63;
            }

            float patternsLeft = L.Patterns.X + 6; // visual left edge of first channel column

            if(sndFile.CurrentPattern > 0) RenderPattern(g, ref r, patternsLeft, fromChannel, sndFile.Order[sndFile.CurrentPattern - 1], 64, r.Y - sfRow * monoFontSize.Height, false);
            RenderPattern(g, ref r, patternsLeft, fromChannel, patternIndex, sfRow, r.Y, true);
            if(sndFile.NextPattern != 0xFF) RenderPattern(g, ref r, patternsLeft, fromChannel, sndFile.Order[sndFile.NextPattern], 0, r.Y - (sfRow - 64) * monoFontSize.Height, false);

            g.FillRectangle(Brushes.LightGrey, patternsLeft - 6, 0, r.Width + 6, monoFontSize.Height);
            float bottom = L.Patterns.Bottom;
            for(int chn = 0; chn < maxChannels; chn++) {
                float cx = patternsLeft + chn * channelWidth;
                g.DrawText(monoFont, Brushes.DarkSlateBlue, cx + (channelWidth - monoFontSize.Width * 8) / 2, 0, $"Channel {chn + fromChannel + 1}");
                g.DrawLine(Pens.DimGray, cx - 6, 0, cx - 6, bottom);
            }
        }

        private void RenderSamples(Graphics g, Layout L) {
            float sampleX = monoFontSize.Width * sampleCharsCount;
            int sampleW = 200;
            int sampleH = (int)monoFontSize.Height + 2;
            EnsureSampleBitmaps(sampleW, sampleH);
            int panelW = (int)Math.Ceiling(L.Samples.Width);
            int panelH = (int)Math.Ceiling(L.Samples.Height);
            EnsureSamplesPanelBitmap(panelW, panelH, sampleX, sampleW, sampleH);
            if(samplesPanelBitmap != null) g.DrawImage(samplesPanelBitmap, 0, 0);

            float y = (int)(monoFontSize.Height * 1.5);
            for(int i = 1; i < sndFile.Instruments.Length; i++) {
                SoundFile.ModInstrument inst = sndFile.Instruments[i];
                if(inst.Sample != null) {
                    RectangleF cursorRect = new(sampleX, y - sampleH, sampleW, sampleH);
                    Renderer.RenderInstrumentCursors(sndFile, i, g, cursorRect);
                }
                y += sampleH + 4;
            }
        }

        // Bakes the static samples-panel content (instrument names, waveform blits,
        // horizontal row dividers, vertical column dividers) into a single bitmap so
        // each frame's samples region is a single blit plus the live cursor lines.
        // Rebuilt when the panel size changes or the loaded SoundFile changes.
        private void EnsureSamplesPanelBitmap(int panelW, int panelH, float sampleX, int sampleW, int sampleH) {
            if(panelW <= 0 || panelH <= 0) return;
            if(samplesPanelBitmap != null && samplesPanelW == panelW && samplesPanelH == panelH) return;
            samplesPanelBitmap?.Dispose();
            samplesPanelBitmap = new Bitmap(panelW, panelH, PixelFormat.Format32bppRgba);
            samplesPanelW = panelW;
            samplesPanelH = panelH;
            using Graphics bg = new(samplesPanelBitmap);
            bg.AntiAlias = true;
            float y = (int)(monoFontSize.Height * 1.5);
            for(int i = 1; i < sndFile.Instruments.Length; i++) {
                SoundFile.ModInstrument inst = sndFile.Instruments[i];
                string n = inst.Name;
                if(n.Length >= sampleCharsCount) n = string.Concat(n.AsSpan(0, sampleCharsCount - 2), "…");
                bg.DrawText(monoFont, Brushes.White, 0, y - sampleH, n);
                if(inst.Sample != null) {
                    Bitmap wf = sampleWaveformBitmaps[i];
                    if(wf != null) bg.DrawImage(wf, sampleX, y - sampleH);
                }
                bg.DrawLine(Pens.DimGray, 0, y + 1, sampleX + sampleW, y + 1);
                y += sampleH + 4;
            }
            bg.DrawLine(Pens.DimGray, sampleX - 4, 0, sampleX - 4, panelH);
            bg.DrawLine(Pens.DimGray, sampleX + sampleW, 0, sampleX + sampleW, panelH);
        }

        // Lazily renders each instrument's sample waveform into a small bitmap so
        // subsequent paints can blit a static image instead of recomputing thousands of
        // line segments per frame. Bitmaps are sized by the layout-derived (w, h) and
        // rebuilt only when those change or the loaded file changes.
        private void EnsureSampleBitmaps(int w, int h) {
            if(w <= 0 || h <= 0) return;
            int n = sndFile.Instruments.Length;
            if(sampleWaveformBitmaps.Length != n || sampleBitmapW != w || sampleBitmapH != h) {
                ResetSampleBitmapCache();
                sampleWaveformBitmaps = new Bitmap[n];
                sampleBitmapW = w;
                sampleBitmapH = h;
            }
            for(int i = 1; i < n; i++) {
                if(sampleWaveformBitmaps[i] != null) continue;
                SoundFile.ModInstrument inst = sndFile.Instruments[i];
                if(inst.Sample == null) continue;
                Bitmap bmp = new(w, h, PixelFormat.Format32bppRgba);
                using(Graphics bg = new(bmp)) {
                    bg.AntiAlias = true;
                    Renderer.RenderInstrumentSample(inst, bg, cWfPen, w, h);
                }
                sampleWaveformBitmaps[i] = bmp;
            }
        }

        private void RenderProgress(Graphics g, Layout L) {
            RectangleF r = L.Progress;
            progressRect = r;
            g.FillRectangle(Brushes.DimGray, r);
            r.Width = (int)(r.Width * (double)sndFile.Position / sndFile.PositionCount);
            g.FillRectangle(progressBrush, r);
        }

        private void RenderWaveform(Graphics g, Layout L) {
            RectangleF r = L.Waveform;
            if(!userHasDroppedFile) g.DrawText(monoFont, Brushes.Gray, r.Location, "Drop a new MOD file\nto start playing it");
            Renderer.RenderWaveform(sndFile, buffer, g, oWfPenL, oWfPenR, r);
        }

        private void RenderPattern(Graphics g, ref RectangleF r, float patternsLeft, int fromChannel, uint patternIndex, int sfRow, float y, bool active) {
            float rowH = monoFontSize.Height;
            float colW = monoFontSize.Width - 1;
            SolidBrush[] rowBg = bkColor[active ? 0 : 1];
            float totalW = channelWidth * maxChannels;
            EnsureEmptyCellBitmaps();
            Bitmap emptyBmp = active ? emptyCellActiveBitmap : emptyCellInactiveBitmap;
            for(int row = 0; row < 64; row++) {
                float yo = y - (sfRow - row) * rowH;
                if(yo < 0) continue;
                if(yo >= r.Height) break;
                bool highlight = (row == sfRow) || (row % 4) == 0;
                if(highlight) g.FillRectangle(rowBg[row == sfRow ? 1 : 0], patternsLeft, yo, totalW, rowH);

                for(int chn = 0; chn < maxChannels; chn++) {
                    string[] cmds = GetCmdTokens(patternIndex, (uint)row, chn + fromChannel);
                    float rx = patternsLeft + chn * channelWidth;
                    if(ReferenceEquals(cmds, emptyTokens)) {
                        if(emptyBmp != null) g.DrawImage(emptyBmp, rx, yo);
                        continue;
                    }
                    float xo = 0;
                    for(int i = 0; i < cmds.Length; i++) {
                        string s = cmds[i];
                        g.DrawText(monoFont, cColor[active ? i + 1 : 0], rx + xo, yo, s);
                        xo += (s.Length + 1) * colW;
                    }
                }
            }
        }

        // Bakes the "... .. ... ..." placeholder into a small bitmap per style so
        // wholly-empty cells (the bulk of any pattern) render as a single blit.
        private void EnsureEmptyCellBitmaps() {
            int w = (int)Math.Ceiling(channelWidth);
            int h = (int)Math.Ceiling(monoFontSize.Height);
            if(emptyCellActiveBitmap != null && emptyCellW == w && emptyCellH == h) return;
            emptyCellActiveBitmap?.Dispose();
            emptyCellInactiveBitmap?.Dispose();
            emptyCellW = w;
            emptyCellH = h;
            emptyCellActiveBitmap = BuildEmptyCellBitmap(w, h, true);
            emptyCellInactiveBitmap = BuildEmptyCellBitmap(w, h, false);
        }

        private Bitmap BuildEmptyCellBitmap(int w, int h, bool active) {
            Bitmap bmp = new(w, h, PixelFormat.Format32bppRgba);
            using Graphics bg = new(bmp);
            float colW = monoFontSize.Width - 1;
            float xo = 0;
            for(int i = 0; i < emptyTokens.Length; i++) {
                string s = emptyTokens[i];
                bg.DrawText(monoFont, cColor[active ? i + 1 : 0], xo, 0, s);
                xo += (s.Length + 1) * colW;
            }
            return bmp;
        }

        private string[] GetCmdTokens(uint pattern, uint row, int channel) {
            if(pattern == 0xFF || pattern >= cmdTokensCache.Length) return emptyTokens;
            string[][] perPattern = cmdTokensCache[pattern];
            uint activeChannels = sndFile.ActiveChannels;
            if(perPattern == null) {
                perPattern = new string[64 * activeChannels][];
                cmdTokensCache[pattern] = perPattern;
            }
            int idx = (int)(row * activeChannels) + channel;
            if((uint)idx >= (uint)perPattern.Length) return emptyTokens;
            string[] tokens = perPattern[idx];
            if(tokens == null) {
                tokens = sndFile.CommandToString(pattern, row, channel).Split(' ');
                bool allEmpty = true;
                for(int i = 0; i < tokens.Length; i++) {
                    string s = tokens[i];
                    if(s.Length > 0 && s[0] != '.') { allEmpty = false; break; }
                }
                if(allEmpty) tokens = emptyTokens;
                perPattern[idx] = tokens;
            }
            return tokens;
        }

        private RectangleF DisplayRectangle() {
            return new RectangleF(PointF.Empty, this.ClientSize);
        }
    }
}
