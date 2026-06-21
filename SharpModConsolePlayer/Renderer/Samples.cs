using SharpMod;
using PrettyConsole;
using static PrettyConsole.Color;

namespace SharpModConsolePlayer.Renderer {
    internal static class Samples {
        private const int HeaderRow = Info.InfoRow + 1;
        internal const int FirstSampleRow = HeaderRow + 3;
        private const int NameWidth = 28;
        // Widths of the fixed columns rendered by RenderSample:
        //   base prefix = " {index,2}  {name,-28}"                                      = 33
        //   extra meta  = "    {Length,6}   {Vol,3}   {Fmt,4}  {LoopStart,9}  {LoopEnd,7}" = 43
        private const int BasePrefixWidth = 33;
        private const int ExtraMetadataWidth = 43;
        private const int FullMetadataWidth = BasePrefixWidth + ExtraMetadataWidth;
        private const int WaveformLeftMargin = 2;

        // Number of waveform rows drawn per sample entry; supported values are 0 (hide the
        // waveform entirely, one compact row per sample), 1 (waveform shares the row with the
        // metadata), 2 (name top-aligned with the 2 waveform rows) and 3 (name centered on the
        // middle of the 3 waveform rows). Settable by the host so a CLI flag can override the
        // default.
        internal static int RowsPerSample { get; set; } = 2;

        // When false (the default) only the index and name precede the waveform, freeing the
        // Length/Vol/Fmt/LoopStart/LoopEnd column block for additional waveform real estate.
        internal static bool ShowMetadata { get; set; } = false;

        // Per-instrument cached braille rows. The waveform itself never changes while a track
        // plays, so we render each sample at most once and reuse the strings across frames.
        // Invalidated whenever the song, terminal width, row count, or metadata-visibility
        // changes (the layout would shift and existing entries would no longer line up).
        private static SoundFile? cachedSoundFile;
        private static int cachedWidth;
        private static int cachedWaveformRowCount;
        private static bool cachedShowMetadata;
        private static int cachedFromSample;
        private static string[]?[] waveformCache = [];
        // Per visible-slot list of cursor character columns drawn on the previous frame.
        // Diffing against the current frame's cursor columns lets us only touch the cells
        // that actually changed, keeping high-FPS frames cheap on the terminal.
        private static int[]?[] cursorCache = [];

        internal static void Render(SoundFile sf, int fromSample, bool forceRedraw) {
            int width = Console.WindowWidth;
            int height = Console.WindowHeight - 1; // reserve last row for the song progress bar
            if(width <= 0) return;
            int waveformRowCount = Math.Clamp(RowsPerSample, 0, 3);
            int rowsPerSample = Math.Max(1, waveformRowCount);

            bool waveformCacheStale = !ReferenceEquals(sf, cachedSoundFile)
                || width != cachedWidth
                || waveformRowCount != cachedWaveformRowCount
                || ShowMetadata != cachedShowMetadata;
            bool fullRedraw = forceRedraw || waveformCacheStale || fromSample != cachedFromSample;

            if(waveformCacheStale) {
                int n = sf.Instruments?.Length ?? 0;
                waveformCache = new string[n][];
            }
            cachedSoundFile = sf;
            cachedWidth = width;
            cachedWaveformRowCount = waveformRowCount;
            cachedShowMetadata = ShowMetadata;
            cachedFromSample = fromSample;

            int total = sf.Instruments != null ? sf.Instruments.Length - 1 : 0;
            int maxConsoleRows = Math.Max(0, height - FirstSampleRow);
            int maxSamples = maxConsoleRows / rowsPerSample;
            int rendered = Math.Max(0, Math.Min(total - fromSample, maxSamples));

            if(cursorCache.Length != rendered) {
                cursorCache = new int[rendered][];
                fullRedraw = true;
            }

            if(fullRedraw) RenderHeader(sf, width);

            for(int i = 0; i < rendered; i++) {
                RenderSample(sf, i, i + 1 + fromSample, FirstSampleRow + i * rowsPerSample, width, waveformRowCount, fullRedraw);
            }
            if(fullRedraw) {
                int firstClearRow = FirstSampleRow + rendered * rowsPerSample;
                int lastRow = FirstSampleRow + maxConsoleRows;
                for(int r = firstClearRow; r < lastRow; r++) {
                    ClearRow(r, width);
                }
            }
        }

        private static void RenderHeader(SoundFile sf, int width) {
            Console.SetCursorPosition(0, HeaderRow);
            uint seconds = sf.Length;
            string lengthValue = seconds >= 3600
                ? $"{seconds / 3600}:{seconds / 60 % 60:D2}:{seconds % 60:D2}"
                : $"{seconds / 60}:{seconds % 60:D2}";
            string bpmValue = $"{sf.AverageTempo}";

            int prefixWidth = 2 + Info.TitleWidth + 1; // " │" + title spaces + "|"
            int remaining = Math.Max(0, width - prefixWidth);
            string s0 = Channel.ClipSegment(" Length: ", ref remaining);
            string s1 = Channel.ClipSegment("~", ref remaining);
            string s2 = Channel.ClipSegment(lengthValue, ref remaining);
            string s3 = Channel.ClipSegment("  BPM: ", ref remaining);
            string s4 = Channel.ClipSegment("~", ref remaining);
            string s5 = Channel.ClipSegment(bpmValue, ref remaining);

            Console.WriteLineInterpolated($"{Default}{Magenta} │{new WhiteSpace(Info.TitleWidth)}{DarkGray}|{Cyan}{s0}{DarkGray}{s1}{White}{s2}{Cyan}{s3}{DarkGray}{s4}{White}{s5}{Default}");
            Console.WriteInterpolated($"{Default}{Magenta} └{sf.FileName}{Default}");

            Console.SetCursorPosition(0, HeaderRow + 2);
            string text = ShowMetadata
                ? $"  #  {"Name",-NameWidth}    {"Length",6}   {"Vol",3}   {"Fmt",4}  {"LoopStart",9}  {"LoopEnd",7}"
                : $"  #  {"Name",-NameWidth}";
            if(text.Length > width) text = text[..width];
            int pad = Math.Max(0, width - text.Length);
            Console.WriteInterpolated($"{Default}{Yellow}{text}{new WhiteSpace(pad)}{Default}");
        }

        private static void RenderSample(SoundFile sf, int slot, int index, int row, int width, int waveformRowCount, bool fullRedraw) {
            var ins = sf.Instruments[index];
            bool empty = ins.Length == 0 || ins.Sample == null;
            int rowsPerSample = Math.Max(1, waveformRowCount);
            int metadataSubRow = waveformRowCount == 3 ? 1 : 0;
            int prefixWidth = ShowMetadata ? FullMetadataWidth : BasePrefixWidth;
            int waveformCharWidth = waveformRowCount == 0 ? 0 : Math.Max(0, width - prefixWidth - WaveformLeftMargin);
            int waveformPxWidth = waveformCharWidth * 2;

            string[] waveformRows = GetOrRenderWaveformRows(ins, index, empty, waveformCharWidth, waveformRowCount);
            int[] cursorPixels = (empty || waveformCharWidth == 0 || waveformRowCount == 0)
                ? []
                : ComputeCursorPixels(sf, index, waveformPxWidth);

            if(fullRedraw) {
                PaintSampleFull(ins, index, row, width, waveformRowCount, rowsPerSample, metadataSubRow, prefixWidth, waveformCharWidth, empty, waveformRows, cursorPixels);
            } else {
                int[] prev = cursorCache[slot] ?? [];
                PaintCursorDelta(row, width, rowsPerSample, prefixWidth, waveformRows, prev, cursorPixels);
            }
            cursorCache[slot] = cursorPixels;
        }

        // Look up or lazily render the braille rows for the given sample under the current
        // layout. Empty/no-data instruments and the 0-row layout share a fast blank path that
        // doesn't allocate per-frame strings.
        private static string[] GetOrRenderWaveformRows(SoundFile.ModInstrument ins, int index, bool empty, int waveformCharWidth, int waveformRowCount) {
            if(empty || waveformCharWidth == 0 || waveformRowCount == 0) {
                string[] blanks = new string[waveformRowCount];
                string blank = new(' ', waveformCharWidth);
                for(int i = 0; i < waveformRowCount; i++) blanks[i] = blank;
                return blanks;
            }
            if(index < waveformCache.Length && waveformCache[index] is { } cached) return cached;
            string[] rows = RenderWaveform(ins, waveformCharWidth, waveformRowCount);
            if(index < waveformCache.Length) waveformCache[index] = rows;
            return rows;
        }

        private static void PaintSampleFull(SoundFile.ModInstrument ins, int index, int row, int width, int waveformRowCount, int rowsPerSample, int metadataSubRow, int prefixWidth, int waveformCharWidth, bool empty, string[] waveformRows, int[] cursorPixels) {
            string name = SanitizeName(ins.Name ?? string.Empty);
            if(name.Length > NameWidth) name = name[..NameWidth];
            else name = name.PadRight(NameWidth);

            AnsiToken nameColor = empty ? DarkGray : White;
            AnsiToken numColor = empty ? DarkGray : Cyan;
            AnsiToken fmtColor = empty ? DarkGray : Blue;
            string fmt = empty ? "    " : $"{(ins.Is16Bit ? "16" : " 8")}/{(ins.IsStereo ? "S" : "M")}";

            for(int r = 0; r < rowsPerSample; r++) {
                Console.SetCursorPosition(0, row + r);
                if(r == metadataSubRow) {
                    if(waveformCharWidth > 0) {
                        if(ShowMetadata) {
                            Console.WriteInterpolated($"{Default}{DarkGray} {index,2}  {nameColor}{name}    {numColor}{ins.Length,6}   {Green}{ins.Volume,3}   {fmtColor}{fmt,4}  {Magenta}{ins.LoopStart,9}  {ins.LoopEnd,7}{Default}{new WhiteSpace(WaveformLeftMargin)}{Cyan}{waveformRows[r]}{Default}");
                        } else {
                            Console.WriteInterpolated($"{Default}{DarkGray} {index,2}  {nameColor}{name}{Default}{new WhiteSpace(WaveformLeftMargin)}{Cyan}{waveformRows[r]}{Default}");
                        }
                    } else {
                        string metaLine = ShowMetadata
                            ? $" {index,2}  {name}    {ins.Length,6}   {ins.Volume,3}   {fmt,4}  {ins.LoopStart,9}  {ins.LoopEnd,7}"
                            : $" {index,2}  {name}";
                        int pad = Math.Max(0, width - metaLine.Length);
                        if(ShowMetadata) {
                            Console.WriteInterpolated($"{Default}{DarkGray} {index,2}  {nameColor}{name}    {numColor}{ins.Length,6}   {Green}{ins.Volume,3}   {fmtColor}{fmt,4}  {Magenta}{ins.LoopStart,9}  {ins.LoopEnd,7}{Default}{new WhiteSpace(pad)}");
                        } else {
                            Console.WriteInterpolated($"{Default}{DarkGray} {index,2}  {nameColor}{name}{Default}{new WhiteSpace(pad)}");
                        }
                    }
                } else {
                    if(waveformCharWidth > 0) {
                        Console.WriteInterpolated($"{Default}{new WhiteSpace(prefixWidth + WaveformLeftMargin)}{Cyan}{waveformRows[r]}{Default}");
                    } else {
                        Console.WriteInterpolated($"{Default}{new WhiteSpace(width)}");
                    }
                }
            }
            Span<int> cellCols = stackalloc int[32];
            Span<byte> cellMasks = stackalloc byte[32];
            int cellCount = GroupCursorsByCell(cursorPixels, cellCols, cellMasks);
            for(int k = 0; k < cellCount; k++) {
                int col = cellCols[k];
                int absCol = prefixWidth + WaveformLeftMargin + col;
                if(absCol >= width) continue;
                PaintCursorCell(absCol, row, rowsPerSample, cellMasks[k], waveformRows, col);
            }
        }

        // Repaint only the cells whose cursor state changed since the previous frame. Cursors
        // are tracked at pixel resolution (2 pixels per braille cell), grouped per cell into a
        // (col, sideMask) pair where sideMask bit 0 = cursor on the left dot column and bit 1
        // = cursor on the right dot column. Cells whose mask changes get the composite glyph
        // repainted; cells removed entirely get the cached waveform glyph (in cyan) written
        // back; cells with identical state are left untouched so the terminal sees the minimum
        // possible write volume.
        private static void PaintCursorDelta(int row, int width, int rowsPerSample, int prefixWidth, string[] waveformRows, int[] prev, int[] curr) {
            if(waveformRows.Length == 0) return;
            Span<int> prevCols = stackalloc int[32];
            Span<byte> prevMasks = stackalloc byte[32];
            int prevN = GroupCursorsByCell(prev, prevCols, prevMasks);
            Span<int> currCols = stackalloc int[32];
            Span<byte> currMasks = stackalloc byte[32];
            int currN = GroupCursorsByCell(curr, currCols, currMasks);

            for(int i = 0; i < prevN; i++) {
                int col = prevCols[i];
                int absCol = prefixWidth + WaveformLeftMargin + col;
                if(absCol >= width) continue;
                int j = -1;
                for(int k = 0; k < currN; k++) if(currCols[k] == col) { j = k; break; }
                if(j < 0) {
                    RestoreWaveformCell(absCol, row, rowsPerSample, waveformRows, col);
                } else if(currMasks[j] != prevMasks[i]) {
                    PaintCursorCell(absCol, row, rowsPerSample, currMasks[j], waveformRows, col);
                }
            }
            for(int j = 0; j < currN; j++) {
                int col = currCols[j];
                int absCol = prefixWidth + WaveformLeftMargin + col;
                if(absCol >= width) continue;
                bool inPrev = false;
                for(int k = 0; k < prevN; k++) if(prevCols[k] == col) { inPrev = true; break; }
                if(!inPrev) PaintCursorCell(absCol, row, rowsPerSample, currMasks[j], waveformRows, col);
            }
        }

        // Collapse a deduped list of cursor pixel columns into per-cell (charCol, sideMask)
        // entries. sideMask bit 0 = left-of-cell pixel, bit 1 = right-of-cell pixel; two
        // cursors landing on opposite halves of the same cell merge into mask 3 (full block).
        private static int GroupCursorsByCell(int[] pixels, Span<int> cols, Span<byte> masks) {
            int n = 0;
            for(int i = 0; i < pixels.Length && n < cols.Length; i++) {
                int px = pixels[i];
                int col = px >> 1;
                byte sideBit = (byte)(1 << (px & 1));
                int existing = -1;
                for(int k = 0; k < n; k++) if(cols[k] == col) { existing = k; break; }
                if(existing >= 0) masks[existing] |= sideBit;
                else { cols[n] = col; masks[n] = sideBit; n++; }
            }
            return n;
        }

        // Paint a single character cell whose dot column(s) are occupied by play-head cursors.
        // The cursor's side fills all four vertical dots in that column (the 0x47 / 0xB8 masks
        // for left / right), while the *other* side keeps the underlying waveform dots so the
        // shape remains visible. The whole cell renders in yellow since terminals only support
        // one foreground colour per character.
        private static void PaintCursorCell(int absCol, int row, int rowsPerSample, byte sideMask, string[] waveformRows, int col) {
            byte cursorDots = sideMask switch { 1 => 0x47, 2 => 0xB8, _ => 0xFF };
            for(int r = 0; r < rowsPerSample; r++) {
                Console.SetCursorPosition(absCol, row + r);
                char composite;
                if(r < waveformRows.Length && col < waveformRows[r].Length) {
                    byte underlying = (byte)(waveformRows[r][col] - 0x2800);
                    composite = (char)(0x2800 | (underlying & ~cursorDots) | cursorDots);
                } else {
                    composite = (char)(0x2800 | cursorDots);
                }
                Console.WriteInterpolated($"{Yellow}{composite}{Default}");
            }
        }

        // Restore a cell to its cached waveform glyph (no cursor overlay).
        private static void RestoreWaveformCell(int absCol, int row, int rowsPerSample, string[] waveformRows, int col) {
            for(int r = 0; r < rowsPerSample; r++) {
                Console.SetCursorPosition(absCol, row + r);
                char glyph = waveformRows[r][col];
                Console.WriteInterpolated($"{Cyan}{glyph}{Default}");
            }
        }

        // Render the sample data as braille glyph rows. Each console cell packs a 2x4 pixel
        // grid via Unicode code points U+2800..U+28FF, so the visible canvas is
        // (cellWidth * 2) pixels wide by (rowsPerSample * 4) pixels tall. The fill style is
        // min/max-per-column (like btop's CPU graph), which preserves the perceived envelope
        // even at very aggressive downsampling ratios.
        private static string[] RenderWaveform(SoundFile.ModInstrument ins, int cellWidth, int rowsPerSample) {
            int pxWidth = cellWidth * 2;
            int pxHeight = rowsPerSample * 4;
            byte[,] grid = new byte[rowsPerSample, cellWidth];

            byte[] data = ins.Sample;
            bool is16 = ins.Is16Bit;
            bool stereo = ins.IsStereo;
            int frameStride = (is16 ? 2 : 1) * (stereo ? 2 : 1);
            int frameCount = (int)ins.Length;
            int frameBytes = data.Length / frameStride;
            if(frameCount > frameBytes) frameCount = frameBytes;

            if(frameCount > 0 && pxWidth > 0) {
                for(int p = 0; p < pxWidth; p++) {
                    int s0 = (int)((long)p * frameCount / pxWidth);
                    int s1 = (int)((long)(p + 1) * frameCount / pxWidth);
                    if(s1 <= s0) s1 = s0 + 1;
                    if(s1 > frameCount) s1 = frameCount;

                    float vmin = float.MaxValue, vmax = float.MinValue;
                    for(int s = s0; s < s1; s++) {
                        float v = ReadSampleFrame(data, s, is16, stereo);
                        if(v < vmin) vmin = v;
                        if(v > vmax) vmax = v;
                    }
                    if(vmin > vmax) { vmin = 0f; vmax = 0f; }

                    int yTop = (int)Math.Round((1f - vmax) * 0.5f * (pxHeight - 1));
                    int yBot = (int)Math.Round((1f - vmin) * 0.5f * (pxHeight - 1));
                    if(yTop < 0) yTop = 0;
                    if(yBot >= pxHeight) yBot = pxHeight - 1;
                    if(yTop > yBot) (yTop, yBot) = (yBot, yTop);

                    for(int y = yTop; y <= yBot; y++) SetDot(grid, p, y);
                }
            }

            string[] rows = new string[rowsPerSample];
            for(int r = 0; r < rowsPerSample; r++) {
                char[] buf = new char[cellWidth];
                for(int x = 0; x < cellWidth; x++) buf[x] = (char)(0x2800 | grid[r, x]);
                rows[r] = new string(buf);
            }
            return rows;
        }

        // Unicode braille pattern dot layout inside a 2x4 cell:
        //   row 0: dot1=0x01 (left) dot4=0x08 (right)
        //   row 1: dot2=0x02        dot5=0x10
        //   row 2: dot3=0x04        dot6=0x20
        //   row 3: dot7=0x40        dot8=0x80
        private static void SetDot(byte[,] grid, int px, int py) {
            int cx = px >> 1;
            int cy = py >> 2;
            if(cx < 0 || cy < 0 || cy >= grid.GetLength(0) || cx >= grid.GetLength(1)) return;
            int sx = px & 1;
            int sy = py & 3;
            int bit = sy < 3 ? (sx == 0 ? (1 << sy) : (1 << (sy + 3)))
                             : (sx == 0 ? 0x40 : 0x80);
            grid[cy, cx] |= (byte)bit;
        }

        // Decode one sample frame to a normalized float in [-1, 1]. The engine stores 8-bit
        // sample bytes such that interpreting them as sbyte yields the signed value (the
        // C669 loader flips the bias at load time; MOD/STM/S3M/XM already store signed),
        // so a single conversion path works for every format. Stereo frames are averaged
        // since each row is too narrow to be worth splitting L/R like the WinForms view.
        private static float ReadSampleFrame(byte[] data, int frame, bool is16, bool stereo) {
            int stride = (is16 ? 2 : 1) * (stereo ? 2 : 1);
            int off = frame * stride;
            if(off + stride > data.Length) return 0f;
            if(is16) {
                float l = (short)(data[off] | (data[off + 1] << 8)) / 32768f;
                if(stereo) {
                    float r = (short)(data[off + 2] | (data[off + 3] << 8)) / 32768f;
                    return (l + r) * 0.5f;
                }
                return l;
            }
            float l8 = (sbyte)data[off] / 128f;
            if(stereo) {
                float r8 = (sbyte)data[off + 1] / 128f;
                return (l8 + r8) * 0.5f;
            }
            return l8;
        }

        // Returns the distinct pixel columns (within the waveform area, 2 pixels per braille
        // cell) where channel play-heads should be drawn. Cursors track the raw sample position
        // over the full sample length so they sweep the whole waveform rather than just the
        // loop region. Pixel resolution lets the cursor advance half-cell at a time, doubling
        // the perceived smoothness compared to a character-aligned overlay.
        private static int[] ComputeCursorPixels(SoundFile sf, int instrumentIndex, int pxWidth) {
            if(pxWidth <= 0) return [];
            var channels = sf.Channels;
            Span<int> tmp = stackalloc int[32];
            int n = 0;
            for(int c = 0; c < channels.Length && n < tmp.Length; c++) {
                var ch = channels[c];
                if(ch.Length == 0 || ch.InstrumentIndex != (uint)instrumentIndex) continue;
                if(ch.Pos >= ch.Length) continue;
                int px = (int)(ch.Pos * pxWidth / ch.Length);
                if(px < 0) px = 0;
                if(px >= pxWidth) px = pxWidth - 1;
                bool dup = false;
                for(int k = 0; k < n; k++) if(tmp[k] == px) { dup = true; break; }
                if(!dup) tmp[n++] = px;
            }
            int[] result = new int[n];
            for(int i = 0; i < n; i++) result[i] = tmp[i];
            return result;
        }

        private static void ClearRow(int row, int width) {
            Console.SetCursorPosition(0, row);
            Console.WriteInterpolated($"{Default}{new WhiteSpace(width)}");
        }

        // Sample names are decoded from raw CP437 bytes and may contain embedded
        // C0 control characters (NUL, BS, ESC, …) that render as zero columns or
        // get swallowed by the terminal's ANSI parser, throwing every following
        // row out of alignment. Replace them with spaces so visible width matches
        // string length. Lazy allocation: clean names pass through unchanged.
        private static string SanitizeName(string s) {
            char[]? buf = null;
            for(int i = 0; i < s.Length; i++) {
                char c = s[i];
                if(c < 0x20 || c == 0x7F) {
                    buf ??= s.ToCharArray();
                    buf[i] = ' ';
                }
            }
            return buf == null ? s : new string(buf);
        }
    }
}
