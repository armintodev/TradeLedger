using System.Globalization;
using System.Text.Json;
using TradeLedger.Core.Backtesting.Indicators;

namespace TradeLedger.Core.Backtesting.Rules;

public static class RuleDocumentParser
{
    public const int MaxDepth = 20;
    public const int MaxOffset = 500;
    public const int MaxOperands = 20;
    public const decimal MinStopPercent = 0.1m;
    public const decimal MaxStopPercent = 50m;

    private static readonly Dictionary<string, RuleOperator> Operators =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["And"] = RuleOperator.And,
            ["Or"] = RuleOperator.Or,
            ["Not"] = RuleOperator.Not,
            ["GreaterThan"] = RuleOperator.GreaterThan,
            ["GreaterOrEqual"] = RuleOperator.GreaterOrEqual,
            ["LessThan"] = RuleOperator.LessThan,
            ["LessOrEqual"] = RuleOperator.LessOrEqual,
            ["EqualTo"] = RuleOperator.EqualTo,
            ["NotEqualTo"] = RuleOperator.NotEqualTo,
            ["CrossesAbove"] = RuleOperator.CrossesAbove,
            ["CrossesBelow"] = RuleOperator.CrossesBelow,
            ["Between"] = RuleOperator.Between,
            ["RisingFor"] = RuleOperator.RisingFor,
            ["FallingFor"] = RuleOperator.FallingFor,
        };

    public static RuleDocument Parse(string json)
    {
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new RuleValidationException("$", $"The rule is not valid JSON: {ex.Message}");
        }

        using (document)
        {
            return ParseRoot(document.RootElement);
        }
    }

    private static RuleDocument ParseRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new RuleValidationException("$", "The rule must be a JSON object.");
        }

        var version = RequiredInt(root, "version", "$.version");

        if (version != RuleDocument.SupportedVersion)
        {
            throw new RuleValidationException(
                "$.version",
                $"Version {version} is not supported; this build understands version {RuleDocument.SupportedVersion}.");
        }

        var indicators = ParseIndicators(root);
        var declared = indicators.ToDictionary(i => i.Id, StringComparer.OrdinalIgnoreCase);

        if (!root.TryGetProperty("entry", out var entry) || entry.ValueKind != JsonValueKind.Object)
        {
            throw new RuleValidationException("$.entry", "An entry object is required.");
        }

        var longRule = ParseOptionalNode(entry, "long", "$.entry.long", declared, 0);
        var shortRule = ParseOptionalNode(entry, "short", "$.entry.short", declared, 0);

        if (longRule is null && shortRule is null)
        {
            throw new RuleValidationException(
                "$.entry",
                "At least one of entry.long or entry.short must be present; a strategy that cannot enter is not a strategy.");
        }

        var stopLoss = ParseStopLoss(root, declared);

        return new RuleDocument(version, indicators, new EntryRules(longRule, shortRule), stopLoss);
    }

    private static List<IndicatorSpec> ParseIndicators(JsonElement root)
    {
        if (!root.TryGetProperty("indicators", out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            throw new RuleValidationException("$.indicators", "An indicators array is required.");
        }

        var specs = new List<IndicatorSpec>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var index = 0;

        foreach (var element in array.EnumerateArray())
        {
            var path = $"$.indicators[{index}]";
            index++;

            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new RuleValidationException(path, "Each indicator must be an object.");
            }

            var id = RequiredString(element, "id", $"{path}.id");

            if (!seen.Add(id))
            {
                throw new RuleValidationException($"{path}.id", $"Duplicate indicator id '{id}'.");
            }

            var type = RequiredString(element, "type", $"{path}.type");

            if (!IndicatorFactory.IsKnown(type))
            {
                throw new RuleValidationException(
                    $"{path}.type",
                    $"Unknown indicator '{type}'. Supported: {string.Join(", ", IndicatorFactory.SupportedTypes)}.");
            }

            var takesSource = IndicatorFactory.TakesSource(type);
            var source = PriceSource.Close;

            if (element.TryGetProperty("source", out var sourceElement)
                && sourceElement.ValueKind != JsonValueKind.Null)
            {
                if (!takesSource)
                {
                    throw new RuleValidationException(
                        $"{path}.source",
                        $"{type} derives from the whole bar and takes no source.");
                }

                source = ParsePriceSource(sourceElement, $"{path}.source");
            }

            var period = ParsePeriod(element, path);

            specs.Add(new IndicatorSpec(id, type, source, period));
        }

        if (specs.Count == 0)
        {
            throw new RuleValidationException(
                "$.indicators", "At least one indicator must be declared.");
        }

        return specs;
    }

    private static int ParsePeriod(JsonElement element, string path)
    {
        if (!element.TryGetProperty("params", out var parameters)
            || parameters.ValueKind != JsonValueKind.Object)
        {
            throw new RuleValidationException($"{path}.params", "A params object with a period is required.");
        }

        foreach (var property in parameters.EnumerateObject())
        {
            if (!string.Equals(property.Name, "period", StringComparison.OrdinalIgnoreCase))
            {
                throw new RuleValidationException(
                    $"{path}.params.{property.Name}",
                    "The only supported parameter is 'period'.");
            }
        }

        var period = RequiredInt(parameters, "period", $"{path}.params.period");

        if (period < IndicatorFactory.MinPeriod || period > IndicatorFactory.MaxPeriod)
        {
            throw new RuleValidationException(
                $"{path}.params.period",
                $"Period must be between {IndicatorFactory.MinPeriod} and {IndicatorFactory.MaxPeriod}.");
        }

        return period;
    }

    private static RuleNode? ParseOptionalNode(
        JsonElement parent,
        string property,
        string path,
        IReadOnlyDictionary<string, IndicatorSpec> declared,
        int depth)
    {
        if (!parent.TryGetProperty(property, out var element)
            || element.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return ParseNode(element, path, declared, depth);
    }

    private static RuleNode ParseNode(
        JsonElement element,
        string path,
        IReadOnlyDictionary<string, IndicatorSpec> declared,
        int depth)
    {
        if (depth > MaxDepth)
        {
            throw new RuleValidationException(
                path, $"Condition nesting is deeper than {MaxDepth} levels.");
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new RuleValidationException(path, "A condition must be an object.");
        }

        var opName = RequiredString(element, "op", $"{path}.op");

        if (!Operators.TryGetValue(opName, out var op))
        {
            throw new RuleValidationException(
                $"{path}.op",
                $"Unknown operator '{opName}'. Supported: {string.Join(", ", Operators.Keys)}.");
        }

        return op switch
        {
            RuleOperator.And or RuleOperator.Or =>
                ParseLogical(element, path, declared, depth, op),
            RuleOperator.Not => new NotNode(
                ParseRequiredNode(element, "operand", $"{path}.operand", declared, depth + 1)),
            RuleOperator.Between => new BetweenNode(
                ParseOperand(element, "left", $"{path}.left", declared),
                ParseOperand(element, "low", $"{path}.low", declared),
                ParseOperand(element, "high", $"{path}.high", declared)),
            RuleOperator.RisingFor or RuleOperator.FallingFor =>
                ParseTrend(element, path, declared, op),
            _ => new ComparisonNode(
                op,
                ParseOperand(element, "left", $"{path}.left", declared),
                ParseOperand(element, "right", $"{path}.right", declared)),
        };
    }

    private static RuleNode ParseLogical(
        JsonElement element,
        string path,
        IReadOnlyDictionary<string, IndicatorSpec> declared,
        int depth,
        RuleOperator op)
    {
        if (!element.TryGetProperty("operands", out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            throw new RuleValidationException($"{path}.operands", $"{op} needs an operands array.");
        }

        var nodes = new List<RuleNode>();
        var index = 0;

        foreach (var child in array.EnumerateArray())
        {
            nodes.Add(ParseNode(child, $"{path}.operands[{index}]", declared, depth + 1));
            index++;
        }

        if (nodes.Count < 2)
        {
            throw new RuleValidationException(
                $"{path}.operands", $"{op} needs at least two operands.");
        }

        if (nodes.Count > MaxOperands)
        {
            throw new RuleValidationException(
                $"{path}.operands", $"{op} accepts at most {MaxOperands} operands.");
        }

        return new LogicalNode(op, nodes);
    }

    private static RuleNode ParseTrend(
        JsonElement element,
        string path,
        IReadOnlyDictionary<string, IndicatorSpec> declared,
        RuleOperator op)
    {
        var operand = ParseOperand(element, "operand", $"{path}.operand", declared);
        var bars = RequiredInt(element, "bars", $"{path}.bars");

        if (bars < 1 || bars > MaxOffset)
        {
            throw new RuleValidationException(
                $"{path}.bars", $"bars must be between 1 and {MaxOffset}.");
        }

        return new TrendNode(op, operand, bars);
    }

    private static RuleNode ParseRequiredNode(
        JsonElement parent,
        string property,
        string path,
        IReadOnlyDictionary<string, IndicatorSpec> declared,
        int depth)
    {
        if (!parent.TryGetProperty(property, out var element))
        {
            throw new RuleValidationException(path, "This condition is required.");
        }

        return ParseNode(element, path, declared, depth);
    }

    private static RuleOperand ParseOperand(
        JsonElement parent,
        string property,
        string path,
        IReadOnlyDictionary<string, IndicatorSpec> declared)
    {
        if (!parent.TryGetProperty(property, out var element))
        {
            throw new RuleValidationException(path, "This operand is required.");
        }

        return ParseOperandElement(element, path, declared);
    }

    private static RuleOperand ParseOperandElement(
        JsonElement element,
        string path,
        IReadOnlyDictionary<string, IndicatorSpec> declared)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return new ConstantOperand(element.GetDecimal());
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new RuleValidationException(
                path, "An operand must be an object with ref, price or const.");
        }

        var offset = 0;

        if (element.TryGetProperty("offset", out var offsetElement)
            && offsetElement.ValueKind != JsonValueKind.Null)
        {
            offset = offsetElement.ValueKind == JsonValueKind.Number
                ? offsetElement.GetInt32()
                : throw new RuleValidationException($"{path}.offset", "offset must be a number.");

            if (offset < 0 || offset > MaxOffset)
            {
                throw new RuleValidationException(
                    $"{path}.offset",
                    $"offset must be between 0 and {MaxOffset}. A negative offset would read the future.");
            }
        }

        if (element.TryGetProperty("const", out var constant))
        {
            return constant.ValueKind == JsonValueKind.Number
                ? new ConstantOperand(constant.GetDecimal())
                : throw new RuleValidationException($"{path}.const", "const must be a number.");
        }

        if (element.TryGetProperty("price", out var price))
        {
            return new PriceOperand(ParsePriceSource(price, $"{path}.price"), offset);
        }

        if (element.TryGetProperty("ref", out var reference))
        {
            var id = reference.GetString();

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new RuleValidationException($"{path}.ref", "ref must name an indicator.");
            }

            if (!declared.TryGetValue(id, out var spec))
            {
                throw new RuleValidationException(
                    $"{path}.ref", $"No indicator with id '{id}' is declared.");
            }

            string? output = null;

            if (element.TryGetProperty("output", out var outputElement)
                && outputElement.ValueKind != JsonValueKind.Null)
            {
                output = outputElement.GetString();

                if (!IndicatorFactory.HasOutput(spec.Type, output))
                {
                    throw new RuleValidationException(
                        $"{path}.output",
                        $"{spec.Type} has no output '{output}'. Available: {string.Join(", ", IndicatorFactory.OutputsFor(spec.Type))}.");
                }
            }
            else if (IndicatorFactory.OutputsFor(spec.Type).Count > 1)
            {
                throw new RuleValidationException(
                    $"{path}.output",
                    $"{spec.Type} has several outputs, so one must be named: {string.Join(", ", IndicatorFactory.OutputsFor(spec.Type))}.");
            }

            return new IndicatorOperand(spec.Id, output, offset);
        }

        throw new RuleValidationException(
            path, "An operand must carry exactly one of ref, price or const.");
    }

    private static StopLossRule ParseStopLoss(
        JsonElement root,
        IReadOnlyDictionary<string, IndicatorSpec> declared)
    {
        if (!root.TryGetProperty("stopLoss", out var element)
            || element.ValueKind != JsonValueKind.Object)
        {
            throw new RuleValidationException(
                "$.stopLoss",
                "A stop loss is required. Positions close only on the stop, the target or liquidation.");
        }

        var kind = RequiredString(element, "kind", "$.stopLoss.kind");

        if (string.Equals(kind, "Percent", StringComparison.OrdinalIgnoreCase))
        {
            var percent = RequiredDecimal(element, "percent", "$.stopLoss.percent");

            if (percent < MinStopPercent || percent > MaxStopPercent)
            {
                throw new RuleValidationException(
                    "$.stopLoss.percent",
                    $"A percentage stop must be between {MinStopPercent} and {MaxStopPercent}.");
            }

            return new PercentStop(percent);
        }

        if (string.Equals(kind, "IndicatorLevel", StringComparison.OrdinalIgnoreCase))
        {
            var operand = ParseOperandElement(element, "$.stopLoss", declared);

            if (operand is not IndicatorOperand indicator)
            {
                throw new RuleValidationException(
                    "$.stopLoss.ref", "An IndicatorLevel stop must reference an indicator.");
            }

            var buffer = 0m;

            if (element.TryGetProperty("bufferPercent", out var bufferElement)
                && bufferElement.ValueKind == JsonValueKind.Number)
            {
                buffer = bufferElement.GetDecimal();

                if (buffer < 0 || buffer > MaxStopPercent)
                {
                    throw new RuleValidationException(
                        "$.stopLoss.bufferPercent",
                        $"bufferPercent must be between 0 and {MaxStopPercent}.");
                }
            }

            return new IndicatorLevelStop(
                indicator.Ref, indicator.Output, indicator.Offset, buffer);
        }

        throw new RuleValidationException(
            "$.stopLoss.kind",
            $"Unknown stop kind '{kind}'. Supported: Percent, IndicatorLevel.");
    }

    private static PriceSource ParsePriceSource(JsonElement element, string path)
    {
        var value = element.GetString();

        return Enum.TryParse<PriceSource>(value, ignoreCase: true, out var source)
            ? source
            : throw new RuleValidationException(
                path,
                $"Unknown price '{value}'. Supported: {string.Join(", ", Enum.GetNames<PriceSource>())}.");
    }

    private static string RequiredString(JsonElement parent, string property, string path)
    {
        if (!parent.TryGetProperty(property, out var element)
            || element.ValueKind != JsonValueKind.String)
        {
            throw new RuleValidationException(path, "A non-empty string is required.");
        }

        var value = element.GetString();

        return string.IsNullOrWhiteSpace(value)
            ? throw new RuleValidationException(path, "A non-empty string is required.")
            : value.Trim();
    }

    private static int RequiredInt(JsonElement parent, string property, string path)
    {
        if (!parent.TryGetProperty(property, out var element)
            || element.ValueKind != JsonValueKind.Number
            || !element.TryGetInt32(out var value))
        {
            throw new RuleValidationException(path, "A whole number is required.");
        }

        return value;
    }

    private static decimal RequiredDecimal(JsonElement parent, string property, string path)
    {
        if (!parent.TryGetProperty(property, out var element)
            || element.ValueKind != JsonValueKind.Number)
        {
            throw new RuleValidationException(path, "A number is required.");
        }

        return element.GetDecimal();
    }

    public static string CanonicalHash(string json)
    {
        using var document = JsonDocument.Parse(json);
        var canonical = Canonicalise(document.RootElement);

        return Convert.ToHexStringLower(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(canonical)));
    }

    private static string Canonicalise(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(
            ",",
            element.EnumerateObject()
                .OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonicalise(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(
            ",", element.EnumerateArray().Select(Canonicalise)) + "]",
        JsonValueKind.String => JsonSerializer.Serialize(element.GetString()),
        JsonValueKind.Number => element.GetDecimal().ToString(CultureInfo.InvariantCulture),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        _ => "null",
    };
}
