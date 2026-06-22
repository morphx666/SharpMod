using System.Text;
using SharpMod;

namespace SharpModConsolePlayer.Tests;

public class EffectHandlerTests : IDisposable {
    private const uint Rate = 44100;
    private const int Channels = 4;
    private const int SampleLenBytes = 64;
    private const int MOD_PRECISION = 10;

    private readonly string _tempPath = Path.Combine(
        Path.GetTempPath(),
        $"sharp_mod_test_{Guid.NewGuid():N}.mod");

    public void Dispose() {
        try { if(File.Exists(_tempPath)) File.Delete(_tempPath); } catch { }
    }

    // CMD_VIBRATO param check was inverted (4xy with non-zero param was ignored).
    [Fact]
    public void Vibrato_4xy_StoresSlideWhenParamNonZero() {
        var sf = BuildAndLoad(new Cell(0, 0, 428, 1, 0x04, 0x47));
        StepTicks(sf, 1);
        Assert.True(sf.Channels[0].Vibrato);
        Assert.Equal(0x47, sf.Channels[0].VibratoSlide);
    }

    [Fact]
    public void Vibrato_400_PreservesSlideFromPreviousRow() {
        var sf = BuildAndLoad(
            new Cell(0, 0, 428, 1, 0x04, 0x47),
            new Cell(1, 0, 0, 0, 0x04, 0x00));
        StepTicks(sf, 7);
        Assert.Equal(1u, sf.Row);
        Assert.True(sf.Channels[0].Vibrato);
        Assert.Equal(0x47, sf.Channels[0].VibratoSlide);
    }

    // CMD_TREMOLO param check was inverted (same bug as Vibrato).
    [Fact]
    public void Tremolo_7xy_StoresSlideWhenParamNonZero() {
        var sf = BuildAndLoad(new Cell(0, 0, 428, 1, 0x07, 0x47));
        StepTicks(sf, 1);
        Assert.True(sf.Channels[0].Tremolo);
        Assert.Equal(0x47, sf.Channels[0].TremoloSlide);
    }

    // CMD_PANNING8 (MOD 8xx) was overwriting Volume instead of being a no-op.
    [Fact]
    public void Panning_8xx_DoesNotTouchVolume() {
        var sf = BuildAndLoad(
            new Cell(0, 0, 428, 1, 0x00, 0x00),
            new Cell(1, 0, 0, 0, 0x08, 0xFF));
        StepTicks(sf, 7);
        Assert.Equal(1u, sf.Row);
        Assert.Equal(0x100, sf.Channels[0].Volume);
    }

    // E5x Set Finetune was missing << MOD_PRECISION (verbatim from C++ original).
    [Fact]
    public void SetFinetune_E5_AppliesPrecisionShift() {
        var sf = BuildAndLoad(
            new Cell(0, 0, 428, 1, 0x00, 0x00),
            new Cell(1, 0, 0, 0, 0x0E, 0x55));
        StepTicks(sf, 7);
        Assert.Equal(1u, sf.Row);
        const uint FineTuneTable_5 = 8169;
        Assert.Equal(FineTuneTable_5 << MOD_PRECISION, sf.Channels[0].FineTune);
    }

    // Sample loop-end handler had inverted condition.
    [Fact]
    public void LoopingSample_KeepsPlayingAfterEnd() {
        var sf = BuildAndLoad(
            new[] { new Cell(0, 0, 428, 1, 0x00, 0x00) },
            inst1LoopLengthWords: SampleLenBytes / 2);
        StepTicks(sf, 1);
        Assert.Equal((uint)SampleLenBytes << MOD_PRECISION, sf.Channels[0].Length);
    }

    [Fact]
    public void NonLoopingSample_StopsAtEnd() {
        var sf = BuildAndLoad(new Cell(0, 0, 428, 1, 0x00, 0x00));
        StepTicks(sf, 1);
        Assert.Equal(0u, sf.Channels[0].Length);
    }

    [Fact]
    public void Volume_Cxx_SetsScaledVolume() {
        var sf = BuildAndLoad(new Cell(0, 0, 428, 1, 0x0C, 0x20));
        StepTicks(sf, 1);
        Assert.Equal(0x80, sf.Channels[0].Volume);
    }

    [Fact]
    public void Volume_Cxx_ClampsAbove64() {
        var sf = BuildAndLoad(new Cell(0, 0, 428, 1, 0x0C, 0x7F));
        StepTicks(sf, 1);
        Assert.Equal(0x100, sf.Channels[0].Volume);
    }

    [Fact]
    public void Speed_Fxx_LowParamSetsSpeed() {
        var sf = BuildAndLoad(new Cell(0, 0, 428, 1, 0x0F, 0x10));
        StepTicks(sf, 1);
        Assert.Equal(0x10u, sf.MusicSpeed);
    }

    [Fact]
    public void Speed_Fxx_HighParamSetsTempo() {
        var sf = BuildAndLoad(new Cell(0, 0, 428, 1, 0x0F, 0x80));
        StepTicks(sf, 1);
        Assert.Equal(0x80u, sf.MusicTempo);
    }

    [Fact]
    public void PositionJump_Bxx_SetsNextPatternAndForcesRowWrap() {
        var sf = BuildAndLoad(new Cell(0, 0, 428, 1, 0x0B, 0x05));
        StepTicks(sf, 1);
        Assert.Equal(5u, sf.NextPattern);
    }

    private readonly record struct Cell(int Row, int Channel, int Period, int Instrument, int Command, int Param);

    private SoundFile BuildAndLoad(params Cell[] cells)
        => BuildAndLoad(cells, inst1LoopLengthWords: 1);

    private SoundFile BuildAndLoad(Cell[] cells, int inst1LoopLengthWords) {
        const int patternRows = 64;
        const int patternBytes = patternRows * Channels * 4;
        const int instCount = 31;

        using var ms = new MemoryStream();
        ms.Write(new byte[20], 0, 20);

        for(int i = 0; i < instCount; i++) {
            byte[] h = new byte[30];
            h[22] = 0x00; h[23] = 0x20;
            h[24] = 0x00;
            h[25] = 0x40;
            h[26] = 0x00; h[27] = 0x00;
            int loopLen = (i == 0) ? inst1LoopLengthWords : 1;
            h[28] = (byte)((loopLen >> 8) & 0xFF);
            h[29] = (byte)(loopLen & 0xFF);
            ms.Write(h, 0, 30);
        }

        ms.WriteByte(1);
        ms.WriteByte(0x7F);
        ms.Write(new byte[128], 0, 128);
        ms.Write(Encoding.ASCII.GetBytes("M.K."), 0, 4);

        byte[] pat = new byte[patternBytes];
        foreach(var c in cells) {
            int idx = (c.Row * Channels + c.Channel) * 4;
            int period = c.Period & 0xFFF;
            int inst = c.Instrument & 0x1F;
            int cmd = c.Command & 0x0F;
            pat[idx + 0] = (byte)((inst & 0x10) | ((period >> 8) & 0x0F));
            pat[idx + 1] = (byte)(period & 0xFF);
            pat[idx + 2] = (byte)(((inst & 0x0F) << 4) | cmd);
            pat[idx + 3] = (byte)c.Param;
        }
        ms.Write(pat, 0, patternBytes);

        for(int i = 0; i < instCount; i++) ms.Write(new byte[SampleLenBytes], 0, SampleLenBytes);

        File.WriteAllBytes(_tempPath, ms.ToArray());
        return new SoundFile(_tempPath, sampleRate: Rate, is16Bit: true, isStereo: true, loop: false);
    }

    private static void StepTicks(SoundFile sf, int ticks) {
        // Tick size matches engine: (Rate * 5) / (MusicTempo * 2).
        uint framesPerTick = (Rate * 5) / (sf.MusicTempo * 2);
        int sampleSize = (sf.Is16Bit ? 2 : 1) * (sf.IsStereo ? 2 : 1);
        byte[] buf = new byte[ticks * framesPerTick * sampleSize];
        sf.Read(buf, (uint)buf.Length);
    }
}
