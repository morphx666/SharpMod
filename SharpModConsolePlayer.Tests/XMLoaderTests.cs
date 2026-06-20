using System.Text;
using SharpMod;

namespace SharpModConsolePlayer.Tests;

public class XMLoaderTests : IDisposable {
    private readonly string _tempPath = Path.Combine(
        Path.GetTempPath(),
        $"sharp_mod_xm_{Guid.NewGuid():N}.xm");

    public void Dispose() {
        try { if(File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
    }

    // Engine effect letters as encoded in the S3M/XM cell layout (1 = 'A', ..., 26 = 'Z').
    private const byte D = (byte)('D' - 'A' + 1);
    private const byte G = (byte)('G' - 'A' + 1);
    private const byte H = (byte)('H' - 'A' + 1);
    private const byte Q = (byte)('Q' - 'A' + 1);
    private const byte X = (byte)('X' - 'A' + 1);

    [Fact]
    public void TrackerName_AndRestartPos_AreParsed() {
        var sf = LoadXm(BuildEmpty(trackerName: "MilkyTracker 1.04", restartPos: 3, orders: 5));
        Assert.Equal(SoundFile.Types.XM, sf.Type);
        Assert.Equal("MilkyTracker 1.04", sf.TrackerName);
        Assert.Equal(3u, sf.RestartPos);
    }

    // FT2 itself sometimes stores a RestartPos past the order count; the loader must clamp it
    // so the engine doesn't land on an out-of-range slot when looping.
    [Fact]
    public void RestartPos_OutOfRange_ClampsToZero() {
        var sf = LoadXm(BuildEmpty(restartPos: 50, orders: 2));
        Assert.Equal(0u, sf.RestartPos);
    }

    // XM note 1 = C-1, encoded as ((octave) << 4) | semitone in engine bytes.
    [Theory]
    [InlineData((byte)1,  (byte)0x00)]
    [InlineData((byte)13, (byte)0x10)]
    [InlineData((byte)49, (byte)0x40)]
    [InlineData((byte)96, (byte)0x7B)]
    public void Cell_NoteTrigger_EncodesEngineNote(byte rawNote, byte expected) {
        var c = ReadCell(LoadXm(BuildWithCell(rawNote: rawNote, rawInst: 5)));
        Assert.Equal(0x20, c[0] & 0xE0);
        Assert.Equal(expected, c[1]);
        Assert.Equal(5, c[2]);
    }

    // XM raw note 97 = key off; engine encodes that as 0xFF (note off).
    [Fact]
    public void Cell_KeyOffNote_EncodesAsNoteOff() {
        var c = ReadCell(LoadXm(BuildWithCell(rawNote: 97)));
        Assert.Equal(0x20, c[0] & 0xE0);
        Assert.Equal(0xFF, c[1]);
    }

    // Kxx with no note triggers a note-cut so the engine silences the channel.
    [Fact]
    public void Cell_KCommand_WithoutNote_BecomesNoteCut() {
        var c = ReadCell(LoadXm(BuildWithCell(rawCmd: 0x14)));
        Assert.Equal(0x20, c[0] & 0xE0);
        Assert.Equal(0xFE, c[1]);
    }

    // Kxx alongside a real note trigger must NOT clobber the trigger, or the new note would be cut instantly.
    [Fact]
    public void Cell_KCommand_WithNewNote_DoesNotOverrideTrigger() {
        var c = ReadCell(LoadXm(BuildWithCell(rawNote: 49, rawInst: 1, rawCmd: 0x14)));
        Assert.Equal(0x40, c[1]);
    }

    // Xxy extra-fine porta has no engine slot; folded into CMD_RETRIG (Q) with E1x/E2x sub-effects.
    [Theory]
    [InlineData((byte)0x21, (byte)0x14, (byte)0x14)]
    [InlineData((byte)0x21, (byte)0x23, (byte)0x23)]
    public void Cell_XCommand_MapsToRetrigSubeffect(byte rawCmd, byte rawParam, byte expectedParam) {
        var c = ReadCell(LoadXm(BuildWithCell(rawCmd: rawCmd, rawParam: rawParam)));
        Assert.Equal(0x80, c[0] & 0xE0);
        Assert.Equal(Q, c[4]);
        Assert.Equal(expectedParam, c[5]);
    }

    [Theory]
    [InlineData((byte)0x65, D, (byte)0x05)]    // -y vol slide down (60..6F)
    [InlineData((byte)0x73, D, (byte)0x30)]    // +y vol slide up   (70..7F)
    [InlineData((byte)0x83, Q, (byte)0xB3)]    // Dy fine vol slide down -> EBy
    [InlineData((byte)0x95, Q, (byte)0xA5)]    // Uy fine vol slide up   -> EAy
    [InlineData((byte)0xA4, H, (byte)0x40)]    // Sy set vibrato speed
    [InlineData((byte)0xB7, H, (byte)0x07)]    // Vy vibrato (depth)
    [InlineData((byte)0xC8, X, (byte)0x80)]    // Py set panning
    [InlineData((byte)0xCF, X, (byte)0xFF)]    // CF: pan right rail (nibble 0xF -> 0xFF)
    [InlineData((byte)0xF6, G, (byte)0x60)]    // My tone portamento
    public void Cell_VolumeColumnEffects_TranslateToEngineCommand(byte rawVol, byte expectedCmd, byte expectedParam) {
        var c = ReadCell(LoadXm(BuildWithCell(rawVol: rawVol)));
        Assert.Equal(0x80, c[0] & 0xE0);
        Assert.Equal(expectedCmd, c[4]);
        Assert.Equal(expectedParam, c[5]);
    }

    // Pan slides (Dx/Ex) intentionally drop to no-op: the engine has no CMD_PANNINGSLIDE handler.
    [Theory]
    [InlineData((byte)0xD7)]
    [InlineData((byte)0xE3)]
    public void Cell_VolumeColumn_PanSlides_AreDropped(byte rawVol) {
        var c = ReadCell(LoadXm(BuildWithCell(rawVol: rawVol)));
        Assert.Equal(0, c[0]);
    }

    // 0x10..0x50 in the volume column is the set-volume range and lands in the engine's volume slot.
    [Fact]
    public void Cell_VolumeColumn_SetVolume_RoutesToVolumeSlot() {
        var c = ReadCell(LoadXm(BuildWithCell(rawVol: 0x10 + 0x14)));
        Assert.Equal(0x40, c[0] & 0xE0);
        Assert.Equal(0x14, c[3]);
    }

    // XM Cxx is funnelled through the volume column so the engine handles it like a set-volume cell.
    [Fact]
    public void Cell_Cxx_RoutesToVolumeColumn() {
        var c = ReadCell(LoadXm(BuildWithCell(rawCmd: 0x0C, rawParam: 0x20)));
        Assert.Equal(0x40, c[0] & 0xE0);
        Assert.Equal(0x20, c[3]);
    }

    [Fact]
    public void Cell_Cxx_ClampsAbove0x40() {
        var c = ReadCell(LoadXm(BuildWithCell(rawCmd: 0x0C, rawParam: 0x7F)));
        Assert.Equal(0x40, c[3]);
    }

    // Regression: EncodeXMCell used to omit the channel index from the mode byte, so the
    // engine's `chnIdx = mode & 0x1F` always read 0 and every cell on every channel was
    // routed to channel 0 -- collapsing multi-channel rows into one and only lighting up
    // the first channel's VU meter.
    [Fact]
    public void Cell_ChannelIndex_IsEncodedIntoModeByte() {
        byte[] payload = BuildPackedPattern(
            (0, 0, (byte)49, (byte)1, 0, 0, 0),
            (0, 1, (byte)49, (byte)1, 0, 0, 0),
            (0, 2, (byte)49, (byte)1, 0, 0, 0),
            (0, 3, (byte)49, (byte)1, 0, 0, 0));
        var sf = LoadXm(BuildXmFull(patternPayloads: new[] { payload }));
        for(int ch = 0; ch < 4; ch++) {
            var c = ReadCell(sf, row: 0, ch: ch);
            Assert.Equal(ch, c[0] & 0x1F);                  // channel slot encoded in mode
            Assert.Equal(0x20, c[0] & 0xE0);                // note-present flag
        }
    }

    // Empty cells must stay fully zeroed so the engine's `mode == 0` early-skip preserves
    // per-channel effect memory across rows that have no data on that channel.
    [Fact]
    public void Cell_EmptyOnNonZeroChannel_StaysZeroed() {
        var sf = LoadXm(BuildXmFull(patternPayloads: new[] { EmptyPatternPayload() }));
        for(int ch = 0; ch < 4; ch++) {
            var c = ReadCell(sf, row: 0, ch: ch);
            for(int b = 0; b < 6; b++) Assert.Equal(0, c[b]);
        }
    }

    // When the effect column already carries a real command, the volume column's extended effects
    // must yield (the engine only has a single effect slot per cell).
    [Fact]
    public void Cell_VolColExtended_DefersToExplicitEffectColumn() {
        var c = ReadCell(LoadXm(BuildWithCell(rawVol: 0x65, rawCmd: 0x01, rawParam: 0x10)));
        byte F = (byte)('F' - 'A' + 1);
        Assert.Equal(F, c[4]);
        Assert.Equal(0x10, c[5]);
    }

    // ----- sample-level tests -----

    // XMSample.pan goes straight to ModInstrument.DefaultPan when the loader opts in.
    [Fact]
    public void DefaultPan_FromSampleHeader_IsApplied() {
        var s = new SampleSpec(1, 0, 0, vol: 64, finetune: 0, flags: 0, pan: 64, relnote: 0, reserved: 0, new byte[] { 0 });
        var sf = LoadXm(BuildXmFull(instruments: new[] { new[] { s } }));
        Assert.True(sf.Instruments[1].HasDefaultPan);
        Assert.Equal(64, sf.Instruments[1].DefaultPan);
    }

    // 0xFF is the right rail in XM; the engine's 0..256 range needs a special case to avoid an off-by-one.
    [Fact]
    public void DefaultPan_0xFF_MapsToFullRight() {
        var s = new SampleSpec(1, 0, 0, 64, 0, 0, 0xFF, 0, 0, new byte[] { 0 });
        var sf = LoadXm(BuildXmFull(instruments: new[] { new[] { s } }));
        Assert.Equal(256, sf.Instruments[1].DefaultPan);
    }

    // Stereo XM samples store the left block followed by the right block, each independently delta-encoded.
    // L deltas +1,+2 -> 1, 3. R deltas +10,-5 -> 10, 5.
    [Fact]
    public void StereoSample_DeltaDecodes_LeftThenRight() {
        byte[] delta = {
            0x01, 0x00, 0x02, 0x00,                         // L: +1, +2
            0x0A, 0x00, 0xFB, 0xFF,                         // R: +10, -5
        };
        const byte F16 = (byte)XMTools.XMSample.XMSampleFlags.sample16Bit;
        const byte FStereo = (byte)XMTools.XMSample.XMSampleFlags.sampleStereo;
        var s = new SampleSpec(8, 0, 0, 64, 0, (byte)(F16 | FStereo), 128, 0, 0, delta);
        var sf = LoadXm(BuildXmFull(instruments: new[] { new[] { s } }));
        byte[] sample = sf.Instruments[1].Sample;

        Assert.True(sf.Instruments[1].Is16Bit);
        Assert.True(sf.Instruments[1].IsStereo);
        Assert.Equal(2u, sf.Instruments[1].Length);
        // L block: 1, 3
        Assert.Equal(0x01, sample[0]); Assert.Equal(0x00, sample[1]);
        Assert.Equal(0x03, sample[2]); Assert.Equal(0x00, sample[3]);
        // R block: 10, 5
        Assert.Equal(0x0A, sample[4]); Assert.Equal(0x00, sample[5]);
        Assert.Equal(0x05, sample[6]); Assert.Equal(0x00, sample[7]);
    }

    // ModPlugin's ADPCM (reserved == 0xAD) isn't decoded; the loader must consume the right number
    // of disk bytes (16-byte step table + (N+1)/2 nibble bytes) so subsequent reads stay aligned.
    [Fact]
    public void AdpcmSample_IsSkipped_NextInstrumentStillAligned() {
        const byte ADPCM = (byte)XMTools.XMSample.XMSampleFlags.sampleADPCM;
        var adpcm = new SampleSpec(10, 0, 0, 64, 0, 0, 128, 0, ADPCM, new byte[16 + 5]);
        // 8-bit mono with delta +5 four times -> decoded 5,10,15,20.
        var regular = new SampleSpec(4, 0, 0, 64, 0, 0, 128, 0, 0, new byte[] { 5, 5, 5, 5 });
        var sf = LoadXm(BuildXmFull(instruments: new[] { new[] { adpcm }, new[] { regular } }));

        Assert.Null(sf.Instruments[1].Sample);              // ADPCM block consumed but not decoded
        Assert.Equal(4u, sf.Instruments[2].Length);         // alignment preserved -> 2nd instrument's header parsed correctly
        byte[] data = sf.Instruments[2].Sample;
        Assert.Equal(5,  (sbyte)data[0]);
        Assert.Equal(10, (sbyte)data[1]);
        Assert.Equal(15, (sbyte)data[2]);
        Assert.Equal(20, (sbyte)data[3]);
    }

    // Regression: effect-only cells (mode == 0x80 only) used to reset the channel volume to
    // instruments[0].Volume (= 0) because instIdx defaulted to 0. The fix gates the reset on a
    // real note trigger with an instrument byte.
    [Fact]
    public void EffectOnlyCell_DoesNotResetChannelVolume() {
        var s = new SampleSpec(4, 0, 0, vol: 32, 0, 0, 128, 0, 0, new byte[4]);
        byte[] payload = BuildPackedPattern(
            (row: 0, ch: 0, rawNote: (byte)49, rawInst: (byte)1, rawVol: (byte)0, rawCmd: (byte)0,    rawParam: (byte)0),
            (row: 1, ch: 0, rawNote: (byte)0,  rawInst: (byte)0, rawVol: (byte)0, rawCmd: (byte)0x04, rawParam: (byte)0x07));
        var sf = LoadXm(BuildXmFull(patternPayloads: new[] { payload }, instruments: new[] { new[] { s } }));

        StepTicks(sf, 1);                                   // land on row 0, instrument volume applied
        Assert.Equal(0u, sf.Row);
        Assert.Equal(128, sf.Channels[0].Volume);
        StepTicks(sf, 6);                                   // speed=6 -> next note tick lands on row 1
        Assert.Equal(1u, sf.Row);
        Assert.Equal(128, sf.Channels[0].Volume);
    }

    // When Loop=true and the song runs past the last pattern, the engine must jump back to
    // RestartPos (FT2/OpenMPT semantics) rather than always wrapping to order 0.
    [Fact]
    public void RestartPos_OnLoop_ReturnsToConfiguredOrder() {
        var s = new SampleSpec(2, 0, 0, 64, 0, 0, 128, 0, 0, new byte[2]);
        // Pattern 0 row 0 = Bxx with param=5 (jumps past the order table; engine wraps via Loop path).
        byte[] pat0 = BuildPackedPattern(
            (0, 0, (byte)49, (byte)1, (byte)0, (byte)0x0B, (byte)0x05));
        byte[] pat1 = EmptyPatternPayload();
        var sf = LoadXm(BuildXmFull(
            restartPos: 1, orderTable: new byte[] { 0, 1 },
            patternPayloads: new[] { pat0, pat1 },
            instruments: new[] { new[] { s } }), loop: true);

        StepTicks(sf, 10);                                  // ~7 ticks suffice; pad to be safe
        Assert.Equal(1u, sf.CurrentPattern);
    }

    // ----- helpers -----

    private SoundFile LoadXm(byte[] data, bool loop = false) {
        File.WriteAllBytes(_tempPath, data);
        return new SoundFile(_tempPath, sampleRate: 44100, is16Bit: true, isStereo: true, loop: loop);
    }

    private static void StepTicks(SoundFile sf, int ticks) {
        uint framesPerTick = (sf.Rate * 5) / (sf.MusicTempo * 2);
        int sampleSize = (sf.Is16Bit ? 2 : 1) * (sf.IsStereo ? 2 : 1);
        sf.Read(new byte[ticks * framesPerTick * sampleSize], (uint)(ticks * framesPerTick * sampleSize));
    }

    // XMSample header fields + raw on-disk sample bytes (delta-encoded for regular samples,
    // step-table + nibble payload for ADPCM).
    private readonly record struct SampleSpec(
        uint length, uint loopStart, uint loopLength,
        byte vol, sbyte finetune, byte flags, byte pan, sbyte relnote, byte reserved,
        byte[] data);

    // Build a packed pattern payload from a sparse set of (row, channel, raw fields) entries.
    private static byte[] BuildPackedPattern(params (int row, int ch, byte rawNote, byte rawInst, byte rawVol, byte rawCmd, byte rawParam)[] cells) {
        var byCell = cells.ToDictionary(c => (c.row, c.ch), c => c);
        var buf = new List<byte>();
        for(int row = 0; row < 64; row++) {
            for(int ch = 0; ch < 4; ch++) {
                if(!byCell.TryGetValue((row, ch), out var c)) { buf.Add(0x80); continue; }
                byte info = 0x80;
                var follow = new List<byte>();
                if(c.rawNote  != 0) { info |= 0x01; follow.Add(c.rawNote); }
                if(c.rawInst  != 0) { info |= 0x02; follow.Add(c.rawInst); }
                if(c.rawVol   != 0) { info |= 0x04; follow.Add(c.rawVol); }
                if(c.rawCmd   != 0) { info |= 0x08; follow.Add(c.rawCmd); }
                if(c.rawParam != 0) { info |= 0x10; follow.Add(c.rawParam); }
                buf.Add(info);
                buf.AddRange(follow);
            }
        }
        return buf.ToArray();
    }

    private static byte[] ReadCell(SoundFile sf, int row = 0, int ch = 0) {
        int rowSize = 6 * (int)sf.ActiveChannels;
        byte[] cell = new byte[6];
        Array.Copy(sf.Patterns[0], row * rowSize + ch * 6, cell, 0, 6);
        return cell;
    }

    private static byte[] BuildEmpty(string trackerName = "test", ushort restartPos = 0, ushort orders = 1)
        => BuildXm(trackerName, restartPos, orders, EmptyPatternPayload());

    // Encode one packed cell at (row 0, channel 0) carrying the given raw XM fields, then pad
    // out the remaining 3 channels of row 0 and 63 empty rows with header-only empty cells.
    private static byte[] BuildWithCell(byte rawNote = 0, byte rawInst = 0, byte rawVol = 0, byte rawCmd = 0, byte rawParam = 0) {
        byte info = 0x80;                                   // packed flag byte
        var follow = new List<byte>();
        if(rawNote  != 0) { info |= 0x01; follow.Add(rawNote); }
        if(rawInst  != 0) { info |= 0x02; follow.Add(rawInst); }
        if(rawVol   != 0) { info |= 0x04; follow.Add(rawVol); }
        if(rawCmd   != 0) { info |= 0x08; follow.Add(rawCmd); }
        if(rawParam != 0) { info |= 0x10; follow.Add(rawParam); }

        var packed = new List<byte> { info };
        packed.AddRange(follow);
        for(int ch = 1; ch < 4; ch++) packed.Add(0x80);
        for(int row = 1; row < 64; row++)
            for(int ch = 0; ch < 4; ch++) packed.Add(0x80);
        return BuildXm("test", 0, 1, packed.ToArray());
    }

    private static byte[] EmptyPatternPayload() {
        byte[] payload = new byte[64 * 4];
        for(int i = 0; i < payload.Length; i++) payload[i] = 0x80;
        return payload;
    }

    private static byte[] BuildXm(string trackerName, ushort restartPos, ushort orders, byte[] patternPayload) {
        byte[] orderTable = new byte[orders];               // every slot -> pattern 0
        return BuildXmFull(trackerName, restartPos, orderTable, new[] { patternPayload }, Array.Empty<SampleSpec[]>());
    }

    private static byte[] BuildXmFull(
        string trackerName = "test", ushort restartPos = 0,
        byte[]? orderTable = null, byte[][]? patternPayloads = null,
        SampleSpec[][]? instruments = null) {
        orderTable ??= new byte[] { 0 };
        patternPayloads ??= new[] { EmptyPatternPayload() };
        instruments ??= Array.Empty<SampleSpec[]>();

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        WriteFixed(bw, "Extended Module: ", 17);
        WriteFixed(bw, "song", 20);
        bw.Write((byte)0x1A);
        WriteFixed(bw, trackerName, 20);
        bw.Write((ushort)0x0104);                           // version
        // size covers the rest of the variable header (20 bytes of fields + the order table)
        // because the loader uses (size + 60) as the pattern-data start offset.
        bw.Write((uint)(20 + (uint)orderTable.Length));
        bw.Write((ushort)orderTable.Length);
        bw.Write(restartPos);
        bw.Write((ushort)4);                                // channels
        bw.Write((ushort)patternPayloads.Length);
        bw.Write((ushort)instruments.Length);
        bw.Write((ushort)0);                                // flags
        bw.Write((ushort)6); bw.Write((ushort)125);         // speed / tempo

        bw.Write(orderTable);

        foreach(byte[] payload in patternPayloads) {
            bw.Write((uint)9);                              // pattern header size
            bw.Write((byte)0);                              // packing type
            bw.Write((ushort)64);                           // rows
            bw.Write((ushort)payload.Length);
            bw.Write(payload);
        }

        foreach(SampleSpec[] samples in instruments) WriteInstrument(bw, samples);

        return ms.ToArray();
    }

    private static void WriteInstrument(BinaryWriter bw, SampleSpec[] samples) {
        bw.Write((uint)29);                                 // instSize: minimum 29 bytes, no padding
        bw.Write(new byte[22]);                             // name
        bw.Write((byte)0);                                  // type
        bw.Write((ushort)samples.Length);                   // numSamples (at offset 27..28)

        foreach(SampleSpec s in samples) {
            bw.Write(s.length); bw.Write(s.loopStart); bw.Write(s.loopLength);
            bw.Write(s.vol); bw.Write((byte)s.finetune); bw.Write(s.flags);
            bw.Write(s.pan); bw.Write((byte)s.relnote); bw.Write(s.reserved);
            bw.Write(new byte[22]);                         // sample name
        }
        foreach(SampleSpec s in samples) bw.Write(s.data);
    }

    private static void WriteFixed(BinaryWriter bw, string s, int len) {
        byte[] buf = new byte[len];
        byte[] src = Encoding.ASCII.GetBytes(s);
        Array.Copy(src, buf, Math.Min(src.Length, len));
        bw.Write(buf);
    }
}
