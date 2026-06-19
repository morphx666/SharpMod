using System.Text;
using SharpMod;

namespace SharpModConsolePlayer.Tests;

public class STMLoaderTests : IDisposable {
    private readonly string _tempPath = Path.Combine(
        Path.GetTempPath(),
        $"sharp_mod_stm_{Guid.NewGuid():N}.stm");

    public void Dispose() {
        try { if(File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
    }

    // Real STMs (e.g. THEMODEL.STM, THE_LOOK.STM) fill the reserved field with 0x58 ('X'),
    // which used to match the "XXXX" S3M-magic fallback at offset 0x2C and cause STM files
    // to be misidentified as S3M. STM validation must run before that fallback.
    [Fact]
    public void XxxxReservedFill_IsStillDetectedAsStm() {
        var sf = LoadStm(Build(songName: "the model", fillReservedWith: 0x58));
        Assert.Equal(SoundFile.Types.STM, sf.Type);
        Assert.True(sf.IsValid);
        Assert.Equal("the model", sf.Title);
    }

    [Theory]
    [InlineData("!Scream!")]
    [InlineData("BMOD2STM")]
    [InlineData("WUZAMOD!")]
    [InlineData("SWavePro")]
    public void KnownTrackerSignatures_AreDetectedAsStm(string trackerName) {
        var sf = LoadStm(Build(trackerName: trackerName));
        Assert.Equal(SoundFile.Types.STM, sf.Type);
    }

    [Fact]
    public void Defaults_FourChannelsAndThirtyOneSamples() {
        var sf = LoadStm(Build());
        Assert.Equal(4u, sf.ActiveChannels);
        Assert.Equal(31u, sf.ActiveSamples);
    }

    // verMinor < 21 stores tempo as a literal decimal byte; the loader converts it to BCD
    // before extracting speed from the high nibble. Byte 60 -> 0x60 -> speed 6.
    [Fact]
    public void OldVersion_TempoByteIsBcdConverted() {
        var sf = LoadStm(Build(verMinor: 20, initTempo: 60));
        Assert.Equal(SoundFile.Types.STM, sf.Type);
        Assert.Equal(6u, sf.MusicSpeed);
    }

    // verMinor >= 21 uses the byte directly; high nibble = speed.
    [Fact]
    public void NewVersion_TempoByteHighNibbleIsSpeed() {
        var sf = LoadStm(Build(verMinor: 21, initTempo: 0x60));
        Assert.Equal(6u, sf.MusicSpeed);
    }

    [Fact]
    public void SampleHeader_VolumeIsScaledAndFilenameStored() {
        var sf = LoadStm(Build(configureSample1: true));
        // Loader shifts volume << 2, so 64 -> 256.
        Assert.Equal(256, sf.Instruments[1].Volume);
        Assert.Equal(16u, sf.Instruments[1].Length);
        Assert.Equal("snare.smp", sf.Instruments[1].Name);
        Assert.NotNull(sf.Instruments[1].Sample);
        Assert.Equal(16, sf.Instruments[1].Sample.Length);
    }

    // Cell layout in mPatterns is [mode, note, instrument, volume, command, param] per cell,
    // 4 cells per row, 6 bytes per cell. STM C-3 (0x10) maps to engine note 0x30.
    [Fact]
    public void Cell_NoteInstrumentVolume_DecodedToInternalLayout() {
        var sf = LoadStm(Build(configureSample1: true,
            firstCell: new byte[] { 0x10, 0x08, 0x80, 0x00 }));
        byte[] pat0 = sf.Patterns[0];
        // mode = 0x20 (note) | 0x40 (vol) | channel 0 = 0x60. cmd bit (0x80) is not set.
        Assert.Equal(0x60, pat0[0]);
        Assert.Equal(0x30, pat0[1]);
        Assert.Equal(0x01, pat0[2]);
        Assert.Equal(0x40, pat0[3]);
    }

    // 0xFC empty marker must produce a zero mode-byte and consume no extra bytes.
    [Fact]
    public void Cell_EmptyMarker0xFC_LeavesCellZeroed() {
        var sf = LoadStm(Build());                          // all cells are 0xFC
        byte[] pat0 = sf.Patterns[0];
        for(int i = 0; i < 6; i++) Assert.Equal(0, pat0[i]);
    }

    // The reorder must not regress real S3M files: SCRM at offset 0x2C still wins.
    [Fact]
    public void S3mScrmMagic_StillDetectedAsS3m() {
        File.WriteAllBytes(_tempPath, BuildMinimalS3M());
        var sf = new SoundFile(_tempPath, sampleRate: 44100, is16Bit: true, isStereo: true, loop: false);
        Assert.Equal(SoundFile.Types.S3M, sf.Type);
    }

    // ----- helpers -----

    private SoundFile LoadStm(byte[] data) {
        File.WriteAllBytes(_tempPath, data);
        return new SoundFile(_tempPath, sampleRate: 44100, is16Bit: true, isStereo: true, loop: false);
    }

    private static byte[] Build(
        string songName = "test", string trackerName = "!Scream!",
        byte dosEof = 0x1A, byte verMajor = 2, byte verMinor = 21,
        byte initTempo = 0x60, byte numPatterns = 1, byte globalVolume = 64,
        byte fillReservedWith = 0, bool configureSample1 = false,
        byte[]? firstCell = null) {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        WriteFixed(bw, songName, 20);
        WriteFixed(bw, trackerName, 8);
        bw.Write(dosEof); bw.Write((byte)2); bw.Write(verMajor); bw.Write(verMinor);
        bw.Write(initTempo); bw.Write(numPatterns); bw.Write(globalVolume);
        for(int i = 0; i < 13; i++) bw.Write(fillReservedWith);

        const int sampleDataOffset = 1456;
        for(int i = 1; i <= 31; i++) {
            if(i == 1 && configureSample1) {
                WriteFixed(bw, "snare.smp", 12);
                bw.Write((byte)0); bw.Write((byte)0);
                bw.Write((ushort)(sampleDataOffset / 16));
                bw.Write((ushort)16); bw.Write((ushort)0); bw.Write((ushort)0xFFFF);
                bw.Write((byte)64); bw.Write((byte)0); bw.Write((ushort)8363);
                bw.Write(new byte[6]);
            } else {
                bw.Write(new byte[32]);
            }
        }

        bw.Write((byte)0);                                  // order[0] = pattern 0
        for(int i = 1; i < 128; i++) bw.Write((byte)99);    // sentinels

        int cellsConsumed = 0;
        if(firstCell != null) { bw.Write(firstCell); cellsConsumed = 1; }
        for(int i = cellsConsumed; i < 64 * 4; i++) bw.Write((byte)0xFC);

        while(ms.Position < sampleDataOffset) bw.Write((byte)0);
        bw.Write(new byte[] { 0, 32, 64, 90, 100, 90, 64, 32, 0, 224, 192, 166, 156, 166, 192, 224 });
        return ms.ToArray();
    }

    private static void WriteFixed(BinaryWriter bw, string s, int len) {
        byte[] buf = new byte[len];
        byte[] src = Encoding.ASCII.GetBytes(s);
        Array.Copy(src, buf, Math.Min(src.Length, len));
        bw.Write(buf);
    }

    private static byte[] BuildMinimalS3M() {
        // Minimal S3M header up to offset 0x60 with 0 ordnum/insnum/patnum so the parser
        // does no further reads. "SCRM" must be at offset 0x2C.
        byte[] hdr = new byte[0x60];
        Encoding.ASCII.GetBytes("title").CopyTo(hdr, 0);    // songname
        hdr[0x1C] = 0x1A; hdr[0x1D] = 16; hdr[0x1E] = 0; hdr[0x1F] = 0;
        hdr[0x20] = 0; hdr[0x21] = 0;                       // ordnum = 0
        hdr[0x22] = 0; hdr[0x23] = 0;                       // insnum = 0
        hdr[0x24] = 0; hdr[0x25] = 0;                       // patnum = 0
        Encoding.ASCII.GetBytes("SCRM").CopyTo(hdr, 0x2C);
        return hdr;
    }
}
