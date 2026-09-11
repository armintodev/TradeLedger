using System.Globalization;
using System.Text;
using TradeLedger.Core.Domain.MarketData;

namespace TradeLedger.Core.MarketData;

public sealed record CsvImportResult(
    IReadOnlyList<Candle> Candles,
    IReadOnlyList<string> Warnings,
    int RowsParsed,
    int DuplicateTimestamps);

public sealed class CsvKlineImportException(string message) : Exception(message);

public static class CsvKlineImporter
{
    private const int MaxDuplicateTimestamps = 1000;

    private static readonly string[] TimeColumns =
        ["time", "date", "datetime", "open time", "opentime", "timestamp"];

    private static readonly string[] TimeFormats =
    [
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-dd",
        "MM/dd/yyyy HH:mm:ss",
        "dd/MM/yyyy HH:mm:ss",
    ];

    public static CsvImportResult Parse(
        Stream stream,
        CandleSource source,
        string symbol,
        CandleInterval interval)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var headerLine = reader.ReadLine()
            ?? throw new CsvKlineImportException("The file is empty.");

        var header = SplitRow(headerLine)
            .Select(h => h.Trim().Trim('"').ToLowerInvariant())
            .ToList();

        var timeIndex = IndexOfAny(header, TimeColumns);
        var openIndex = header.IndexOf("open");
        var highIndex = header.IndexOf("high");
        var lowIndex = header.IndexOf("low");
        var closeIndex = header.IndexOf("close");
        var volumeIndex = header.IndexOf("volume");

        if (timeIndex < 0)
        {
            throw new CsvKlineImportException(
                $"No time column found. Expected one of: {string.Join(", ", TimeColumns)}.");
        }

        foreach (var (name, index) in new[]
                 {
                     ("open", openIndex), ("high", highIndex),
                     ("low", lowIndex), ("close", closeIndex),
                 })
        {
            if (index < 0)
            {
                throw new CsvKlineImportException($"Required column '{name}' is missing.");
            }
        }

        var warnings = new List<string>();
        var candles = new List<Candle>();
        var seen = new HashSet<long>();
        var duplicates = 0;
        var rowsParsed = 0;
        var lineNumber = 1;
        var assumedUtc = false;
        DateTimeOffset? previous = null;
        var fetchedAt = DateTimeOffset.UtcNow;

        while (reader.ReadLine() is { } line)
        {
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = SplitRow(line);
            rowsParsed++;

            if (cells.Count <= timeIndex || cells.Count <= closeIndex)
            {
                throw new CsvKlineImportException(
                    $"Line {lineNumber} has {cells.Count} columns, fewer than the header declares.");
            }

            var openTime = ParseTime(cells[timeIndex], lineNumber, ref assumedUtc);

            if (!interval.IsAligned(openTime))
            {
                throw new CsvKlineImportException(
                    $"Line {lineNumber}: {openTime:O} is not aligned to a {interval} boundary.");
            }

            if (previous is { } prior && openTime <= prior)
            {
                if (openTime == prior)
                {
                    duplicates++;

                    if (duplicates > MaxDuplicateTimestamps)
                    {
                        throw new CsvKlineImportException(
                            $"More than {MaxDuplicateTimestamps} duplicate timestamps; the file looks malformed.");
                    }

                    continue;
                }

                throw new CsvKlineImportException(
                    $"Line {lineNumber}: timestamps must increase, but {openTime:O} follows {prior:O}.");
            }

            previous = openTime;

            var open = ParseNumber(cells[openIndex], lineNumber, "open");
            var high = ParseNumber(cells[highIndex], lineNumber, "high");
            var low = ParseNumber(cells[lowIndex], lineNumber, "low");
            var close = ParseNumber(cells[closeIndex], lineNumber, "close");

            var volume = volumeIndex >= 0 && cells.Count > volumeIndex
                ? ParseNumber(cells[volumeIndex], lineNumber, "volume")
                : 0m;

            if (high < low)
            {
                throw new CsvKlineImportException(
                    $"Line {lineNumber}: high {high} is below low {low}.");
            }

            if (open < low || open > high)
            {
                throw new CsvKlineImportException(
                    $"Line {lineNumber}: open {open} is outside the low-high range.");
            }

            if (close < low || close > high)
            {
                throw new CsvKlineImportException(
                    $"Line {lineNumber}: close {close} is outside the low-high range.");
            }

            if (volume < 0)
            {
                throw new CsvKlineImportException($"Line {lineNumber}: volume is negative.");
            }

            if (!seen.Add(openTime.ToUnixTimeMilliseconds()))
            {
                duplicates++;
                continue;
            }

            candles.Add(Candle.Of(
                source,
                symbol,
                interval,
                openTime,
                open,
                high,
                low,
                close,
                volume));
        }

        if (candles.Count == 0)
        {
            throw new CsvKlineImportException("The file contained no data rows.");
        }

        if (assumedUtc)
        {
            warnings.Add(
                "Timestamps carried no time-zone offset and were read as UTC.");
        }

        if (volumeIndex < 0)
        {
            warnings.Add("No volume column was present; volume was recorded as zero.");
        }

        if (duplicates > 0)
        {
            warnings.Add($"{duplicates} duplicate timestamps were skipped.");
        }

        return new CsvImportResult(candles, warnings, rowsParsed, duplicates);
    }

    private static int IndexOfAny(List<string> header, string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var index = header.IndexOf(candidate);

            if (index >= 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static DateTimeOffset ParseTime(string raw, int lineNumber, ref bool assumedUtc)
    {
        var value = raw.Trim().Trim('"');

        if (value.Length == 0)
        {
            throw new CsvKlineImportException($"Line {lineNumber}: the time cell is empty.");
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var epoch))
        {
            return value.Length >= 13
                ? DateTimeOffset.FromUnixTimeMilliseconds(epoch)
                : DateTimeOffset.FromUnixTimeSeconds(epoch);
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            if (!HasOffset(value))
            {
                assumedUtc = true;
            }

            return parsed;
        }

        if (DateTime.TryParseExact(
                value,
                TimeFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var exact))
        {
            assumedUtc = true;
            return new DateTimeOffset(exact, TimeSpan.Zero);
        }

        throw new CsvKlineImportException(
            $"Line {lineNumber}: could not read '{value}' as a date or epoch timestamp.");
    }

    private static bool HasOffset(string value) =>
        value.EndsWith('Z')
        || value.EndsWith("z", StringComparison.Ordinal)
        || (value.Length > 6 && (value[^6] == '+' || value[^6] == '-') && value[^3] == ':')
        || (value.Length > 5 && (value[^5] == '+' || value[^5] == '-'));

    private static decimal ParseNumber(string raw, int lineNumber, string column)
    {
        var value = raw.Trim().Trim('"').Replace("_", string.Empty, StringComparison.Ordinal);

        if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        throw new CsvKlineImportException(
            $"Line {lineNumber}: could not read '{raw}' in column '{column}' as a number.");
    }

    private static List<string> SplitRow(string line)
    {
        var cells = new List<string>();
        var builder = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    builder.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    break;
                case ',' or ';' or '\t':
                    cells.Add(builder.ToString());
                    builder.Clear();
                    break;
                default:
                    builder.Append(c);
                    break;
            }
        }

        cells.Add(builder.ToString());

        return cells;
    }
}
