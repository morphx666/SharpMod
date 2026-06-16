using SharpModConsolePlayer.Renderer;

namespace SharpModConsolePlayer.Tests;

public class ChannelRenderTests {
    private const int RowsPerPattern = Channel.RowsPerPattern;

    // Pattern-relative indices passed to ComputeConsoleRow: -1 = previous, 0 = current, +1 = next.
    private const int Previous = -1;
    private const int Current = 0;
    private const int Next = 1;

    [Theory]
    [InlineData(40, 0)]
    [InlineData(40, 31)]
    [InlineData(40, 63)]
    [InlineData(12, 7)]
    [InlineData(100, 50)]
    public void ActiveRowLandsAtCenter(int center, int currentPatternRow) {
        int consoleRow = Channel.ComputeConsoleRow(center, currentPatternRow, currentPatternRow, Current);
        Assert.Equal(center, consoleRow);
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(40, 31)]
    [InlineData(40, 63)]
    public void CurrentPatternRowZeroIsCenterMinusCurrentRow(int center, int currentPatternRow) {
        int consoleRow = Channel.ComputeConsoleRow(center, currentPatternRow, 0, Current);
        Assert.Equal(center - currentPatternRow, consoleRow);
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(40, 31)]
    [InlineData(40, 63)]
    public void PreviousPatternLastRowIsOneAboveCurrentPatternRowZero(int center, int currentPatternRow) {
        int prevLast = Channel.ComputeConsoleRow(center, currentPatternRow, RowsPerPattern - 1, Previous);
        int currFirst = Channel.ComputeConsoleRow(center, currentPatternRow, 0, Current);
        Assert.Equal(currFirst - 1, prevLast);
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(40, 31)]
    [InlineData(40, 63)]
    public void NextPatternFirstRowIsOneBelowCurrentPatternLastRow(int center, int currentPatternRow) {
        int currLast = Channel.ComputeConsoleRow(center, currentPatternRow, RowsPerPattern - 1, Current);
        int nextFirst = Channel.ComputeConsoleRow(center, currentPatternRow, 0, Next);
        Assert.Equal(currLast + 1, nextFirst);
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(40, 31)]
    [InlineData(40, 63)]
    public void ConsecutiveRowsAreContiguous(int center, int currentPatternRow) {
        for(int row = 0; row < RowsPerPattern - 1; row++) {
            int a = Channel.ComputeConsoleRow(center, currentPatternRow, row, Current);
            int b = Channel.ComputeConsoleRow(center, currentPatternRow, row + 1, Current);
            Assert.Equal(a + 1, b);
        }
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(40, 31)]
    [InlineData(40, 63)]
    public void PreviousPatternSpansAboveCurrent(int center, int currentPatternRow) {
        // The whole previous pattern must sit strictly above the current pattern's row 0.
        int currFirst = Channel.ComputeConsoleRow(center, currentPatternRow, 0, Current);
        for(int row = 0; row < RowsPerPattern; row++) {
            int prevRow = Channel.ComputeConsoleRow(center, currentPatternRow, row, Previous);
            Assert.True(prevRow < currFirst, $"prevRow[{row}]={prevRow} should be < currFirst={currFirst}");
        }
    }

    [Theory]
    [InlineData(40, 0)]
    [InlineData(40, 31)]
    [InlineData(40, 63)]
    public void NextPatternSpansBelowCurrent(int center, int currentPatternRow) {
        // The whole next pattern must sit strictly below the current pattern's last row.
        int currLast = Channel.ComputeConsoleRow(center, currentPatternRow, RowsPerPattern - 1, Current);
        for(int row = 0; row < RowsPerPattern; row++) {
            int nextRow = Channel.ComputeConsoleRow(center, currentPatternRow, row, Next);
            Assert.True(nextRow > currLast, $"nextRow[{row}]={nextRow} should be > currLast={currLast}");
        }
    }

    [Fact]
    public void NoOtherPatternRowCanLandOnCenter() {
        // Given the constraint 0 <= currentPatternRow < RowsPerPattern, no row of the
        // previous or next pattern should coincide with `center`.
        const int center = 50;
        for(int currentPatternRow = 0; currentPatternRow < RowsPerPattern; currentPatternRow++) {
            for(int row = 0; row < RowsPerPattern; row++) {
                int prev = Channel.ComputeConsoleRow(center, currentPatternRow, row, Previous);
                int next = Channel.ComputeConsoleRow(center, currentPatternRow, row, Next);
                Assert.NotEqual(center, prev);
                Assert.NotEqual(center, next);
            }
        }
    }
}
