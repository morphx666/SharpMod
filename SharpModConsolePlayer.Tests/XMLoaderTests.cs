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

    // When the effect column already carries a real command, the volume column's extended effects
    // must yield (the engine only has a single effect slot per cell).
    [Fact]
    public void Cell_VolColExtended_DefersToExplicitEffectColumn() {
        var c = ReadCell(LoadXm(BuildWithCell(rawVol: 0x65, rawCmd: 0x01, rawParam: 0x10)));
        byte F = (byte)('F' - 'A' + 1);
        Assert.Equal(F, c[4]);
        Assert.Equal(0x10, c[5]);
    }

    // ----- helpers -----

    private SoundFile LoadXm(byte[] data) {
        File.WriteAllBytes(_tempPath, data);
        return new SoundFile(_tempPath, sampleRate: 44100, is16Bit: true, isStereo: true, loop: false);
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
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        WriteFixed(bw, "Extended Module: ", 17);
        WriteFixed(bw, "song", 20);
        bw.Write((byte)0x1A);
        WriteFixed(bw, trackerName, 20);
        bw.Write((ushort)0x0104);                           // version
        // size covers the rest of the variable header (20 bytes of fields + the order table)
        // because the loader uses (size + 60) as the pattern-data start offset.
        bw.Write((uint)(20 + (uint)orders));
        bw.Write(orders); bw.Write(restartPos);
        bw.Write((ushort)4);                                // channels
        bw.Write((ushort)1);                                // patterns
        bw.Write((ushort)0);                                // instruments
        bw.Write((ushort)0);                                // flags
        bw.Write((ushort)6); bw.Write((ushort)125);         // speed / tempo

        for(int i = 0; i < orders; i++) bw.Write((byte)0);  // every order slot -> pattern 0

        bw.Write((uint)9);                                  // pattern header size
        bw.Write((byte)0);                                  // packing type
        bw.Write((ushort)64);                               // rows
        bw.Write((ushort)patternPayload.Length);
        bw.Write(patternPayload);

        return ms.ToArray();
    }

    private static void WriteFixed(BinaryWriter bw, string s, int len) {
        byte[] buf = new byte[len];
        byte[] src = Encoding.ASCII.GetBytes(s);
        Array.Copy(src, buf, Math.Min(src.Length, len));
        bw.Write(buf);
    }
}
