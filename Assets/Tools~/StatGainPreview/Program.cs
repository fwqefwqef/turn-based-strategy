using System.Globalization;
using StatGainTools;

const int DefaultStartLevel = 1;
const int DefaultEndLevel = 20;

try
{
    ParsedInput parsedInput = ParseInput(args);
    IReadOnlyList<FormulaKind> formulasToRun = GetFormulasToRun(parsedInput.Formula);
    for (int i = 0; i < formulasToRun.Count; i++)
    {
        FormulaKind formula = formulasToRun[i];
        SequenceResult result = StatGainCalculator.GenerateSequence(
            parsedInput.GrowthRates,
            parsedInput.StartLevel,
            parsedInput.EndLevel,
            formula);

        Console.WriteLine(StatGainCalculator.BuildReport(result, includeHeader: formulasToRun.Count > 1));
        if (i < formulasToRun.Count - 1)
        {
            Console.WriteLine();
        }
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}

static ParsedInput ParseInput(string[] args)
{
    int startLevel = DefaultStartLevel;
    int endLevel = DefaultEndLevel;
    RequestedFormula formula = RequestedFormula.Base;

    List<string> valueTokens = new();
    for (int i = 0; i < args.Length; i++)
    {
        string arg = args[i];
        if (string.Equals(arg, "--end-level", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "-e", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException("Missing value for --end-level.");
            }

            endLevel = ParseNonNegativeInt(args[++i], "end level");
            continue;
        }

        if (string.Equals(arg, "--formula", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "-f", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException("Missing value for --formula.");
            }

            formula = ParseFormulaKind(args[++i]);
            continue;
        }

        if (string.Equals(arg, "--start-level", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(arg, "-s", StringComparison.OrdinalIgnoreCase))
        {
            if (i + 1 >= args.Length)
            {
                throw new ArgumentException("Missing value for --start-level.");
            }

            startLevel = ParseNonNegativeInt(args[++i], "start level");
            continue;
        }

        valueTokens.Add(arg);
    }

    if (valueTokens.Count == 0)
    {
        string? line = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new ArgumentException(
                "Provide 6 growth rates that add up to 100. Example: \"25 20 15 15 15 10\" or use --end-level 30.");
        }

        valueTokens.AddRange(line.Split([' ', '\t', ','], StringSplitOptions.RemoveEmptyEntries));
    }

    if (valueTokens.Count != StatGainCalculator.ExpectedStatCount)
    {
        throw new ArgumentException(
            $"Expected {StatGainCalculator.ExpectedStatCount} growth values in this order: {string.Join(" ", StatGainCalculator.DefaultStatNames)}.");
    }

    int[] growthRates = new int[StatGainCalculator.ExpectedStatCount];
    for (int i = 0; i < StatGainCalculator.ExpectedStatCount; i++)
    {
        growthRates[i] = ParseNonNegativeInt(valueTokens[i], $"growth rate #{i + 1}");
    }

    StatGainCalculator.ValidateGrowthRates(growthRates);

    if (endLevel <= startLevel)
    {
        throw new ArgumentException("End level must be greater than start level.");
    }

    return new ParsedInput(growthRates, startLevel, endLevel, formula);
}

static int ParseNonNegativeInt(string rawValue, string label)
{
    if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) || value < 0)
    {
        throw new ArgumentException($"Invalid {label}: \"{rawValue}\". Expected a natural number.");
    }

    return value;
}

static RequestedFormula ParseFormulaKind(string rawValue)
{
    return rawValue.Trim().ToLowerInvariant() switch
    {
        "base" => RequestedFormula.Base,
        "biased" => RequestedFormula.Biased,
        "both" => RequestedFormula.Both,
        _ => throw new ArgumentException("Invalid formula. Use base, biased, or both.")
    };
}

static IReadOnlyList<FormulaKind> GetFormulasToRun(RequestedFormula formula)
{
    return formula switch
    {
        RequestedFormula.Base => [FormulaKind.Base],
        RequestedFormula.Biased => [FormulaKind.Biased],
        RequestedFormula.Both => [FormulaKind.Base, FormulaKind.Biased],
        _ => throw new ArgumentOutOfRangeException(nameof(formula))
    };
}

internal enum RequestedFormula
{
    Base,
    Biased,
    Both
}

internal readonly record struct ParsedInput(int[] GrowthRates, int StartLevel, int EndLevel, RequestedFormula Formula);
