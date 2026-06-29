using System;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using SharpMod;

public static partial class SharpModInterop {
    private static SoundFile? sf;
    private static byte[] readBuffer = [];

    [JSExport]
    public static string Load(byte[] data, int sampleRate, bool is16Bit, bool stereo, bool loop) {
        try {
            sf = new SoundFile(data, (uint)sampleRate, is16Bit, stereo, loop);
            readBuffer = [];
            return sf.IsValid ? "" : "Unrecognized or invalid module file.";
        } catch(Exception ex) {
            sf = null;
            return ex.Message;
        }
    }

    [JSExport]
    public static byte[] Read(int byteCount) {
        if(sf is null || byteCount <= 0) return [];
        if(readBuffer.Length != byteCount) readBuffer = new byte[byteCount];
        uint read = sf.Read(readBuffer, (uint)byteCount);
        if(read == 0) return [];
        if(read == (uint)byteCount) return readBuffer;
        byte[] trimmed = new byte[read];
        Array.Copy(readBuffer, trimmed, (int)read);
        return trimmed;
    }

    [JSExport] public static bool IsLoaded() => sf is not null;
    [JSExport] public static string GetTitle() => sf?.Title ?? "";
    [JSExport] public static string GetTrackerName() => sf?.TrackerName ?? "";
    [JSExport] public static string GetTypeName() => (sf?.Type ?? SoundFile.Types.INVALID).ToString();
    [JSExport] public static int GetTypeCode() => (int)(sf?.Type ?? SoundFile.Types.INVALID);
    [JSExport] public static int GetSampleRate() => (int)(sf?.Rate ?? 0);
    [JSExport] public static bool GetIs16Bit() => sf?.Is16Bit ?? false;
    [JSExport] public static bool GetIsStereo() => sf?.IsStereo ?? false;
    [JSExport] public static int GetActiveChannels() => (int)(sf?.ActiveChannels ?? 0);
    [JSExport] public static int GetActiveSamples() => (int)(sf?.ActiveSamples ?? 0);
    [JSExport] public static int GetPosition() => (int)(sf?.Position ?? 0);
    [JSExport] public static int GetPositionCount() => (int)(sf?.PositionCount ?? 0);
    [JSExport] public static int GetMusicSpeed() => (int)(sf?.MusicSpeed ?? 0);
    [JSExport] public static int GetMusicTempo() => (int)(sf?.MusicTempo ?? 0);
    [JSExport] public static int GetRow() => (int)(sf?.Row ?? 0);
    [JSExport] public static int GetCurrentPattern() => (int)(sf?.CurrentPattern ?? 0);
    [JSExport] public static int GetPattern() => (int)(sf?.Pattern ?? 0);
    [JSExport] public static int GetNextPattern() => (int)(sf?.NextPattern ?? 0xFF);
    [JSExport] public static int GetLengthSeconds() => (int)(sf?.Length ?? 0);
    [JSExport] public static int GetAverageTempo() => (int)(sf?.AverageTempo ?? 0);
    [JSExport] public static void SetPosition(int pos) { if(sf is not null) sf.Position = (uint)pos; }

    // Order list: returns -1 when the entry is past the end of the song (0xFF terminator).
    [JSExport]
    public static int GetOrderAt(int index) {
        if(sf is null) return -1;
        var order = sf.Order;
        if(index < 0 || index >= order.Length) return -1;
        byte v = order[index];
        return v == 0xFF ? -1 : v;
    }

    // Resolves the pattern index that the patterns view should display: sf.Pattern unless
    // playback has fallen off the end, in which case fall back to the last real pattern in
    // the order list (mirrors ConsoleRenderer.RenderPatterns).
    [JSExport]
    public static int GetDisplayPattern() {
        if(sf is null) return -1;
        uint p = sf.Pattern;
        if(p != 0xFF) return (int)p;
        var order = sf.Order;
        for(int i = order.Length - 1; i >= 0; i--) if(order[i] != 0xFF) return order[i];
        return -1;
    }

    // Per-frame channel snapshot, packed for one round-trip per frame.
    // Layout: 6 ints per active channel, in order:
    //   [muted(0/1), instrumentIndex, currentVolume, pan(0..256), isStereoSample(0/1), isActive(0/1)]
    [JSExport]
    public static int[] GetChannelStates() {
        if(sf is null) return [];
        var ch = sf.Channels;
        int n = Math.Min((int)sf.ActiveChannels, ch.Length);
        int[] r = new int[n * 6];
        for(int i = 0; i < n; i++) {
            var c = ch[i];
            bool active = c.Length > 0 && c.Pos < c.Length;
            int k = i * 6;
            r[k + 0] = c.Muted ? 1 : 0;
            r[k + 1] = (int)c.InstrumentIndex;
            r[k + 2] = c.CurrentVolume;
            r[k + 3] = c.Pan;
            r[k + 4] = c.IsStereo ? 1 : 0;
            r[k + 5] = active ? 1 : 0;
        }
        return r;
    }

    [JSExport] public static bool GetChannelMuted(int idx) {
        if(sf is null || idx < 0 || idx >= sf.ActiveChannels) return false;
        return sf.Channels[idx].Muted;
    }
    [JSExport] public static void ToggleChannelMute(int idx) {
        if(sf is null) return;
        sf.ToggleMute((uint)idx);
    }

    // Returns all 64 rows of a pattern as a single string. Each row is a fixed-width
    // concatenation of CommandToString() output (14 chars per channel), joined with '\n'.
    // A patternIndex of -1 or 0xFF yields a string of blank rows for layout convenience.
    [JSExport]
    public static string GetPatternData(int patternIndex) {
        if(sf is null) return "";
        int nch = (int)sf.ActiveChannels;
        var sb = new StringBuilder(64 * (nch * 14 + 1));
        var pats = sf.Patterns;
        // Treat out-of-range indices and unallocated pattern slots (XM can leave
        // gaps in the pattern array) as empty so the view falls back to placeholders.
        bool empty = patternIndex < 0 || patternIndex >= pats.Length || pats[patternIndex] is null;
        for(int row = 0; row < 64; row++) {
            for(int c = 0; c < nch; c++) {
                sb.Append(empty ? "... .. ... ..." : sf.CommandToString((uint)patternIndex, (uint)row, c));
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    [JSExport] public static int GetInstrumentCount() => sf?.Instruments?.Length ?? 0;
    [JSExport] public static string GetInstrumentName(int index) {
        if(sf is null) return "";
        var ins = sf.Instruments;
        if(index < 0 || index >= ins.Length) return "";
        return ins[index].Name ?? "";
    }

    // Per-instrument metadata. Layout: [length, volume, is16(0/1), isStereo(0/1), loopStart, loopEnd].
    [JSExport]
    public static int[] GetInstrumentMeta(int index) {
        if(sf is null) return [];
        var ins = sf.Instruments;
        if(index < 0 || index >= ins.Length) return [];
        var i = ins[index];
        return new int[] {
            (int)i.Length,
            i.Volume,
            i.Is16Bit ? 1 : 0,
            i.IsStereo ? 1 : 0,
            (int)i.LoopStart,
            (int)i.LoopEnd,
        };
    }

    // Downsamples the instrument's PCM to (width) min/max pairs in [-1, 1], packed as
    // interleaved [min0, max0, min1, max1, ...] of length width*2. Same per-column min/max
    // envelope strategy the console renderer uses for its braille waveform, but at canvas
    // resolution. Returns an empty array for empty / missing instruments.
    [JSExport]
    public static double[] GetWaveformEnvelope(int index, int width) {
        if(sf is null || width <= 0) return [];
        var ins = sf.Instruments;
        if(index < 0 || index >= ins.Length) return [];
        var i = ins[index];
        if(i.Sample is null || i.Length == 0) return [];
        bool is16 = i.Is16Bit;
        bool stereo = i.IsStereo;
        int stride = (is16 ? 2 : 1) * (stereo ? 2 : 1);
        int frameCount = (int)i.Length;
        int frameBytes = i.Sample.Length / stride;
        if(frameCount > frameBytes) frameCount = frameBytes;
        double[] r = new double[width * 2];
        for(int p = 0; p < width; p++) {
            int s0 = (int)((long)p * frameCount / width);
            int s1 = (int)((long)(p + 1) * frameCount / width);
            if(s1 <= s0) s1 = s0 + 1;
            if(s1 > frameCount) s1 = frameCount;
            double vmin = double.MaxValue, vmax = double.MinValue;
            for(int s = s0; s < s1; s++) {
                double v = ReadFrame(i.Sample, s, is16, stereo, stride);
                if(v < vmin) vmin = v;
                if(v > vmax) vmax = v;
            }
            if(vmin > vmax) { vmin = 0; vmax = 0; }
            r[p * 2 + 0] = vmin;
            r[p * 2 + 1] = vmax;
        }
        return r;
    }

    // Distinct playback-position ratios (0..1) for every channel currently sourcing
    // the given instrument. Used by the samples view to draw per-voice cursors over
    // the waveform.
    [JSExport]
    public static double[] GetInstrumentCursors(int index) {
        if(sf is null) return [];
        var channels = sf.Channels;
        Span<double> tmp = stackalloc double[32];
        int n = 0;
        for(int c = 0; c < channels.Length && n < tmp.Length; c++) {
            var ch = channels[c];
            if(ch.Length == 0 || ch.InstrumentIndex != (uint)index) continue;
            if(ch.Pos >= ch.Length) continue;
            double r = (double)ch.Pos / ch.Length;
            bool dup = false;
            for(int k = 0; k < n; k++) if(tmp[k] == r) { dup = true; break; }
            if(!dup) tmp[n++] = r;
        }
        double[] result = new double[n];
        for(int k = 0; k < n; k++) result[k] = tmp[k];
        return result;
    }

    private static double ReadFrame(byte[] data, int frame, bool is16, bool stereo, int stride) {
        int off = frame * stride;
        if(off + stride > data.Length) return 0;
        if(is16) {
            double l = (short)(data[off] | (data[off + 1] << 8)) / 32768.0;
            if(stereo) {
                double r = (short)(data[off + 2] | (data[off + 3] << 8)) / 32768.0;
                return (l + r) * 0.5;
            }
            return l;
        }
        double l8 = (sbyte)data[off] / 128.0;
        if(stereo) {
            double r8 = (sbyte)data[off + 1] / 128.0;
            return (l8 + r8) * 0.5;
        }
        return l8;
    }
}
