using System.Globalization;
using System.Text;
using TradeLedger.Core.Domain.MarketData;
using TradeLedger.Core.MarketData;

namespace TradeLedger.UnitTests.MarketData;

public class CsvKlineImporterTests
{
    private const string Header = "time,open,high,low,close,volume";

    [Fact]
    public void ReadsAWellFormedTradingViewStyleExport()
    {
        var result = Parse($"""
            {Header}
            2025-01-01T00:00:00Z,100,110,95,105,1000
            2025-01-01T01:00:00Z,105,120,104,118,1500
            2025-01-01T02:00:00Z,118,119,110,112,900
            """);

        Assert.Equal(3, result.Candles.Count);
        Assert.Equal(3, result.RowsParsed);

        var first = result.Candles[0];
        Assert.Equal(100m, first.Open);
        Assert.Equal(110m, first.High);
        Assert.Equal(95m, first.Low);
        Assert.Equal(105m, first.Close);
        Assert.Equal(1000m, first.Volume);
        Assert.Equal(CandleSource.CsvImport, first.Source);
        Assert.Equal("BTCUSDT", first.Symbol);
    }

    [Fact]
    public void KeepsFullDecimalPrecisionForSubSatoshiPrices()
    {
        var result = Parse($"""
            {Header}
            2025-01-01T00:00:00Z,0.000000012345678901,0.000000012345678999,0.000000012345678000,0.000000012345678500,1
            """);

        Assert.Equal(0.000000012345678901m, result.Candles[0].Open);
        Assert.Equal(0.000000012345678999m, result.Candles[0].High);
    }

    [Fact]
    public void AcceptsEpochMillisecondTimestamps()
    {
        var result = Parse($"""
            {Header}
            1735689600000,100,110,95,105,10
            1735693200000,105,115,100,110,12
            """);

        Assert.Equal(At("2025-01-01T00:00:00Z"), result.Candles[0].OpenTime);
        Assert.Equal(2, result.Candles.Count);
    }

    [Fact]
    public void AcceptsEpochSecondTimestamps()
    {
        var result = Parse($"""
            {Header}
            1735689600,100,110,95,105,10
            """);

        Assert.Equal(At("2025-01-01T00:00:00Z"), result.Candles[0].OpenTime);
    }

    [Fact]
    public void SaysSoWhenItAssumedUtcBecauseNoOffsetWasGiven()
    {
        var result = Parse($"""
            {Header}
            2025-01-01 00:00:00,100,110,95,105,10
            2025-01-01 01:00:00,105,115,100,110,12
            """);

        Assert.Contains(result.Warnings, w => w.Contains("UTC", StringComparison.Ordinal));
        Assert.Equal(At("2025-01-01T00:00:00Z"), result.Candles[0].OpenTime);
    }

    [Fact]
    public void DoesNotClaimToAssumeUtcWhenAnOffsetWasSupplied()
    {
        var result = Parse($"""
            {Header}
            2025-01-01T00:00:00Z,100,110,95,105,10
            """);

        Assert.DoesNotContain(result.Warnings, w => w.Contains("UTC", StringComparison.Ordinal));
    }

    [Fact]
    public void TreatsVolumeAsZeroAndWarnsWhenTheColumnIsAbsent()
    {
        var result = Parse("""
            time,open,high,low,close
            2025-01-01T00:00:00Z,100,110,95,105
            """);

        Assert.Equal(0m, result.Candles[0].Volume);
        Assert.Contains(result.Warnings, w => w.Contains("volume", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("date")]
    [InlineData("datetime")]
    [InlineData("timestamp")]
    [InlineData("open time")]
    public void AcceptsAnyOfTheKnownTimeColumnNames(string timeColumn)
    {
        var result = Parse($"""
            {timeColumn},open,high,low,close,volume
            2025-01-01T00:00:00Z,100,110,95,105,10
            """);

        Assert.Single(result.Candles);
    }

    [Fact]
    public void IgnoresColumnOrderAndCasing()
    {
        var result = Parse("""
            Close,HIGH,Low,Open,Time,Volume
            105,110,95,100,2025-01-01T00:00:00Z,10
            """);

        Assert.Equal(100m, result.Candles[0].Open);
        Assert.Equal(105m, result.Candles[0].Close);
    }

    [Fact]
    public void AcceptsSemicolonAndTabSeparatedFiles()
    {
        var semicolon = Parse("""
            time;open;high;low;close;volume
            2025-01-01T00:00:00Z;100;110;95;105;10
            """);

        Assert.Single(semicolon.Candles);
    }

    [Fact]
    public void SkipsBlankLines()
    {
        var result = Parse($"""
            {Header}
            2025-01-01T00:00:00Z,100,110,95,105,10

            2025-01-01T01:00:00Z,105,115,100,110,12
            """);

        Assert.Equal(2, result.Candles.Count);
    }

    [Fact]
    public void RejectsAFileWithNoTimeColumn()
    {
        var ex = Assert.Throws<CsvKlineImportException>(() => Parse("""
            open,high,low,close
            100,110,95,105
            """));

        Assert.Contains("time column", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAFileMissingARequiredPriceColumn()
    {
        var ex = Assert.Throws<CsvKlineImportException>(() => Parse("""
            time,open,high,close
            2025-01-01T00:00:00Z,100,110,105
            """));

        Assert.Contains("low", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsTimestampsThatGoBackwards()
    {
        var ex = Assert.Throws<CsvKlineImportException>(() => Parse($"""
            {Header}
            2025-01-01T02:00:00Z,100,110,95,105,10
            2025-01-01T01:00:00Z,105,115,100,110,12
            """));

        Assert.Contains("increase", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsTimestampsNotAlignedToTheDeclaredInterval()
    {
        var ex = Assert.Throws<CsvKlineImportException>(() => Parse($"""
            {Header}
            2025-01-01T00:17:00Z,100,110,95,105,10
            """));

        Assert.Contains("aligned", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAHighBelowItsLow()
    {
        var ex = Assert.Throws<CsvKlineImportException>(() => Parse($"""
            {Header}
            2025-01-01T00:00:00Z,100,90,95,92,10
            """));

        Assert.Contains("below", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAnOpenOutsideTheBarRange()
    {
        var ex = Assert.Throws<CsvKlineImportException>(() => Parse($"""
            {Header}
            2025-01-01T00:00:00Z,200,110,95,105,10
            """));

        Assert.Contains("open", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsACloseOutsideTheBarRange()
    {
        var ex = Assert.Throws<CsvKlineImportException>(() => Parse($"""
            {Header}
            2025-01-01T00:00:00Z,100,110,95,3,10
            """));

        Assert.Contains("close", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsNegativeVolume()
    {
        var ex = Assert.Throws<CsvKlineImportException>(() => Parse($"""
            {Header}
            2025-01-01T00:00:00Z,100,110,95,105,-5
            """));

        Assert.Contains("volume", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAnUnreadableNumber()
    {
        var ex = Assert.Throws<CsvKlineImportException>(() => Parse($"""
            {Header}
            2025-01-01T00:00:00Z,abc,110,95,105,10
            """));

        Assert.Contains("open", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAnUnreadableTimestamp()
    {
        var ex = Assert.Throws<CsvKlineImportException>(() => Parse($"""
            {Header}
            not-a-date,100,110,95,105,10
            """));

        Assert.Contains("epoch", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsAnEmptyFile()
    {
        Assert.Throws<CsvKlineImportException>(() => Parse(string.Empty));
    }

    [Fact]
    public void RejectsAHeaderWithNoDataRows()
    {
        Assert.Throws<CsvKlineImportException>(() => Parse(Header));
    }

    [Fact]
    public void CountsRepeatedTimestampsAsDuplicatesAndKeepsTheFirst()
    {
        var result = Parse($"""
            {Header}
            2025-01-01T00:00:00Z,100,110,95,105,10
            2025-01-01T00:00:00Z,777,888,666,777,99
            2025-01-01T01:00:00Z,105,115,100,110,12
            """);

        Assert.Equal(2, result.Candles.Count);
        Assert.Equal(1, result.DuplicateTimestamps);
        Assert.Equal(100m, result.Candles[0].Open);
        Assert.Contains(result.Warnings, w => w.Contains("duplicate", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParsingTheSameFileTwiceYieldsIdenticalCandles()
    {
        const string csv = """
            time,open,high,low,close,volume
            2025-01-01T00:00:00Z,100,110,95,105,10
            2025-01-01T01:00:00Z,105,115,100,110,12
            """;

        var first = Parse(csv);
        var second = Parse(csv);

        Assert.Equal(
            first.Candles.Select(c => c.OpenTimeRawMs),
            second.Candles.Select(c => c.OpenTimeRawMs));
    }

    private static CsvImportResult Parse(
        string csv,
        CandleInterval interval = CandleInterval.OneHour)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        return CsvKlineImporter.Parse(stream, CandleSource.CsvImport, "BTCUSDT", interval);
    }

    private static DateTimeOffset At(string iso) =>
        DateTimeOffset.Parse(iso, CultureInfo.InvariantCulture);
}
