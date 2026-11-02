using System.Globalization;
using LeaseImport.Common;

namespace LeaseImport.Tests;

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
