namespace StatGainTools;

public enum FormulaKind
{
    Base,
    Biased
}

public readonly record struct LevelUpEntry(int FromLevel, int ToLevel, string FirstStat, string SecondStat);

public sealed record SequenceResult(
    FormulaKind Formula,
    int StartLevel,
    int EndLevel,
    IReadOnlyList<string> StatNames,
    IReadOnlyList<int> GrowthRates,
    IReadOnlyList<LevelUpEntry> Entries,
    IReadOnlyDictionary<string, int> Totals);

public static class StatGainCalculator
{
    public const int ExpectedStatCount = 6;
    public const int ExpectedGrowthTotal = 100;
    public static readonly IReadOnlyList<string> DefaultStatNames =
    [
        "Strength",
        "Magic",
        "Defense",
        "Resistance",
        "Speed",
        "Luck"
    ];

    public static void ValidateGrowthRates(IReadOnlyList<int> growthRates)
    {
        if (growthRates.Count != ExpectedStatCount)
        {
            throw new ArgumentException($"Expected {ExpectedStatCount} growth values.");
        }

        if (growthRates.Any(rate => rate < 0))
        {
            throw new ArgumentException("Growth rates must all be non-negative.");
        }

        if (growthRates.Sum() != ExpectedGrowthTotal)
        {
            throw new ArgumentException($"Growth rates must add up to {ExpectedGrowthTotal}.");
        }
    }

    public static SequenceResult GenerateSequence(
        IReadOnlyList<int> growthRates,
        int startLevel,
        int endLevel,
        FormulaKind formula,
        IReadOnlyList<string>? statNames = null)
    {
        ValidateGrowthRates(growthRates);
        if (endLevel <= startLevel)
        {
            throw new ArgumentException("End level must be greater than start level.");
        }

        IReadOnlyList<string> resolvedStatNames = statNames ?? DefaultStatNames;
        if (resolvedStatNames.Count != ExpectedStatCount)
        {
            throw new ArgumentException($"Expected {ExpectedStatCount} stat names.");
        }

        int levelUpCount = endLevel - startLevel;
        int[] progress = new int[growthRates.Count];
        double[] tieBreakBiases = formula == FormulaKind.Biased
            ? BuildTieBreakBiases(growthRates)
            : new double[growthRates.Count];
        List<LevelUpEntry> entries = new(levelUpCount);

        for (int currentLevel = startLevel; currentLevel < endLevel; currentLevel++)
        {
            for (int i = 0; i < growthRates.Count; i++)
            {
                progress[i] += growthRates[i] * 2;
            }

            int firstIndex = SelectStatIndex(progress, tieBreakBiases, Array.Empty<int>());
            progress[firstIndex] -= ExpectedGrowthTotal;

            int secondIndex = SelectStatIndex(progress, tieBreakBiases, [firstIndex]);
            progress[secondIndex] -= ExpectedGrowthTotal;

            entries.Add(new LevelUpEntry(
                currentLevel,
                currentLevel + 1,
                resolvedStatNames[firstIndex],
                resolvedStatNames[secondIndex]));
        }

        return new SequenceResult(
            formula,
            startLevel,
            endLevel,
            resolvedStatNames.ToArray(),
            growthRates.ToArray(),
            entries,
            SummarizeTotals(entries, resolvedStatNames));
    }

    public static IReadOnlyDictionary<string, int> SummarizeTotals(
        IReadOnlyList<LevelUpEntry> sequence,
        IReadOnlyList<string> statNames)
    {
        Dictionary<string, int> totals = statNames.ToDictionary(name => name, _ => 0);
        foreach (LevelUpEntry entry in sequence)
        {
            totals[entry.FirstStat]++;
            totals[entry.SecondStat]++;
        }

        return totals;
    }

    public static string BuildReport(SequenceResult result, bool includeHeader = false)
    {
        List<string> lines = new();
        if (includeHeader)
        {
            string header = $"{GetFormulaDisplayName(result.Formula)} Formula";
            lines.Add(header);
            lines.Add(new string('-', header.Length));
        }

        foreach (LevelUpEntry entry in result.Entries)
        {
            lines.Add($"Lv {entry.FromLevel} -> Lv {entry.ToLevel}: {entry.FirstStat} +1, {entry.SecondStat} +1");
        }

        lines.Add(string.Empty);
        lines.Add("Total Gains: " + string.Join(", ", result.StatNames.Select(name => $"{name} +{result.Totals[name]}")));
        return string.Join(Environment.NewLine, lines);
    }

    public static string GetFormulaDisplayName(FormulaKind formula)
    {
        return formula switch
        {
            FormulaKind.Base => "Base",
            FormulaKind.Biased => "Biased",
            _ => formula.ToString()
        };
    }

    private static double[] BuildTieBreakBiases(IReadOnlyList<int> growthRates)
    {
        double[] biases = new double[growthRates.Count];
        long seed = 17;
        for (int i = 0; i < growthRates.Count; i++)
        {
            seed = (seed * 131) + growthRates[i] * (i + 3);
        }

        for (int i = 0; i < growthRates.Count; i++)
        {
            long mixed = seed + ((long)(i + 1) * 7919L);
            mixed ^= mixed << 13;
            mixed ^= mixed >> 7;
            mixed ^= mixed << 17;
            int normalized = (int)(Math.Abs(mixed % 1000));
            biases[i] = normalized / 1000.0;
        }

        return biases;
    }

    private static int SelectStatIndex(
        IReadOnlyList<int> progress,
        IReadOnlyList<double> tieBreakBiases,
        IReadOnlyCollection<int> excludedIndices)
    {
        int selectedIndex = -1;
        int selectedProgress = int.MinValue;
        double selectedBias = double.MinValue;

        for (int i = 0; i < progress.Count; i++)
        {
            if (excludedIndices.Contains(i))
            {
                continue;
            }

            double bias = i < tieBreakBiases.Count ? tieBreakBiases[i] : 0d;
            if (progress[i] > selectedProgress || (progress[i] == selectedProgress && bias > selectedBias))
            {
                selectedProgress = progress[i];
                selectedBias = bias;
                selectedIndex = i;
            }
        }

        if (selectedIndex < 0)
        {
            throw new InvalidOperationException("Failed to select a stat for this level-up.");
        }

        return selectedIndex;
    }
}
