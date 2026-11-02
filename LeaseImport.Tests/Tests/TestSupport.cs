using System.Globalization;
using LeaseImport.Common;

namespace LeaseImport.Tests.Tests;

/// <summary>Sets CurrentCulture and CurrentUICulture for the current thread and restores them on Dispose.</summary>
internal sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _culture;
    private readonly CultureInfo _uiCulture;

    public CultureScope(string name)
    {
        _culture = CultureInfo.CurrentCulture;
        _uiCulture = CultureInfo.CurrentUICulture;
        var culture = new CultureInfo(name);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    public void Dispose()
    {
        CultureInfo.CurrentCulture = _culture;
        CultureInfo.CurrentUICulture = _uiCulture;
    }
}

/// <summary>Tests that change the thread culture or the process console never run in parallel with each other.</summary>
[CollectionDefinition("Culture", DisableParallelization = true)]
public sealed class CultureCollection
{
}

internal static class TestData
{
    public const string Header = "LeaseId,AssetClass,Description,Currency,Opening,Additions,Depreciation,Disposals";

    public static string SamplePath => Path.Combine(AppContext.BaseDirectory, "samples", "leases.csv");

    /// <summary>Joins lines with LF (no trailing newline).</summary>
    public static string Csv(params string[] lines) => string.Join("\n", lines);

    public static ImportResult Import(string csv) => LeaseImporter.Import(new StringReader(csv));

    /// <summary>The JSON text exactly as <see cref="LeaseImporter.WriteJson"/> writes it (strict UTF-8 decode).</summary>
    public static string ToJson(ImportResult result)
    {
        using var stream = new MemoryStream();
        LeaseImporter.WriteJson(result, stream);
        return new System.Text.UTF8Encoding(false, true).GetString(stream.ToArray());
    }

    public static ImportResult ImportSample()
    {
        using var reader = new StreamReader(SamplePath, detectEncodingFromByteOrderMarks: true);
        return LeaseImporter.Import(reader);
    }
}

/// <summary>
/// Property-by-property comparison of the model classes (they have no value equality).
/// Amounts are compared as decimals (1.5m equals 1.50m), strings ordinally, nulls exactly.
/// </summary>
internal static class ModelAssert
{
    public static void AssertLease(Lease expected, Lease? actual)
    {
        Assert.NotNull(actual);
        if (!SameLease(expected, actual))
        {
            Assert.Fail($"Lease differs.\nExpected: {Describe(expected)}\nActual:   {Describe(actual)}");
        }
    }

    public static void AssertLeases(IEnumerable<Lease> expected, IEnumerable<Lease> actual)
    {
        var expectedList = expected.ToList();
        var actualList = actual.ToList();
        if (expectedList.Count != actualList.Count || expectedList.Where((e, i) => !SameLease(e, actualList[i])).Any())
        {
            Assert.Fail($"Leases differ.\nExpected:\n{DescribeAll(expectedList, Describe)}\nActual:\n{DescribeAll(actualList, Describe)}");
        }
    }

    public static void AssertProblem(Problem expected, Problem? actual)
    {
        Assert.NotNull(actual);
        if (!SameProblem(expected, actual))
        {
            Assert.Fail($"Problem differs.\nExpected: {Describe(expected)}\nActual:   {Describe(actual)}");
        }
    }

    public static void AssertProblems(IEnumerable<Problem> expected, IEnumerable<Problem> actual)
    {
        var expectedList = expected.ToList();
        var actualList = actual.ToList();
        if (expectedList.Count != actualList.Count || expectedList.Where((e, i) => !SameProblem(e, actualList[i])).Any())
        {
            Assert.Fail($"Problems differ.\nExpected:\n{DescribeAll(expectedList, Describe)}\nActual:\n{DescribeAll(actualList, Describe)}");
        }
    }

    public static void AssertContainsProblem(Problem expected, IEnumerable<Problem> actual)
    {
        var actualList = actual.ToList();
        if (!actualList.Any(p => SameProblem(expected, p)))
        {
            Assert.Fail($"Problem not found.\nExpected: {Describe(expected)}\nIn:\n{DescribeAll(actualList, Describe)}");
        }
    }

    private static bool SameLease(Lease e, Lease a) =>
        e.Line == a.Line
        && string.Equals(e.LeaseId, a.LeaseId, StringComparison.Ordinal)
        && string.Equals(e.AssetClass, a.AssetClass, StringComparison.Ordinal)
        && string.Equals(e.Description, a.Description, StringComparison.Ordinal)
        && string.Equals(e.Currency, a.Currency, StringComparison.Ordinal)
        && e.Opening == a.Opening
        && e.Additions == a.Additions
        && e.Depreciation == a.Depreciation
        && e.Disposals == a.Disposals;

    private static bool SameProblem(Problem e, Problem a) =>
        e.Line == a.Line
        && string.Equals(e.LeaseId, a.LeaseId, StringComparison.Ordinal)
        && string.Equals(e.Field, a.Field, StringComparison.Ordinal)
        && string.Equals(e.Code, a.Code, StringComparison.Ordinal)
        && string.Equals(e.Message, a.Message, StringComparison.Ordinal);

    private static string Describe(Lease l) => string.Create(CultureInfo.InvariantCulture,
        $"Line={l.Line}, LeaseId={Quote(l.LeaseId)}, AssetClass={Quote(l.AssetClass)}, Description={Quote(l.Description)}, " +
        $"Currency={Quote(l.Currency)}, Opening={l.Opening}, Additions={l.Additions}, Depreciation={l.Depreciation}, Disposals={l.Disposals}");

    private static string Describe(Problem p) => string.Create(CultureInfo.InvariantCulture,
        $"Line={p.Line}, LeaseId={Quote(p.LeaseId)}, Field={Quote(p.Field)}, Code={Quote(p.Code)}, Message={Quote(p.Message)}");

    private static string Quote(string? value) => value is null ? "null" : $"\"{value}\"";

    private static string DescribeAll<T>(IReadOnlyList<T> items, Func<T, string> describe) =>
        items.Count == 0 ? "  (none)" : string.Join("\n", items.Select(item => "  " + describe(item)));
}
