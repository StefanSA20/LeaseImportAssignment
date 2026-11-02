using System.Text;
using System.Text.Json;
using LeaseImport.Common;

namespace LeaseImport.Tests;

/// <summary>
/// Imports every samples/&lt;name&gt;.csv that has a hand-written samples/expected/&lt;name&gt;.json
/// (CLAUDE.md section 9) and compares the importer's JSON with it semantically: same property
/// names, equal strings, numbers compared as decimals, arrays in order.
/// The expected files are written by hand from CLAUDE.md, never copied from the importer output.
/// </summary>
/// <remarks>
/// leases-danish-semicolon.csv: the whole ';'-separated header is one column, so every required
/// column is missing and the import is fatal. Its data rows also contain unquoted ',' decimals
/// that would split fields, but rows are never validated after a fatal header, so that doesn't matter.
/// </remarks>
public class SampleFilesIntegrationTests
{
    private static readonly string[] HandWrittenSamples =
    [
        "leases", "leases-valid-dkk", "leases-invalid", "leases-mixed",
        "leases-danish-format", "leases-missing-column", "leases-danish-semicolon",
    ];

    public static TheoryData<string> SamplesWithExpectedJson()
    {
        var data = new TheoryData<string>();
        foreach (var name in FindSamplesWithExpectedJson())
        {
            data.Add(name);
        }
        return data;
    }

    [Fact]
    public void Every_hand_written_sample_has_an_expected_json_file()
    {
        var found = FindSamplesWithExpectedJson();
        foreach (var name in HandWrittenSamples)
        {
            Assert.Contains(name, found);
        }
    }

    [Theory]
    [MemberData(nameof(SamplesWithExpectedJson))]
    public void Importer_output_matches_the_hand_written_expected_json(string name)
    {
        var root = FindRepositoryRoot();
        var csvPath = Path.Combine(root, "samples", name + ".csv");
        var expectedPath = Path.Combine(root, "samples", "expected", name + ".json");

        string actualJson;
        using (var reader = new StreamReader(csvPath, General.Program.StrictUtf8, detectEncodingFromByteOrderMarks: false))
        using (var output = new MemoryStream())
        {
            LeaseImporter.WriteJson(LeaseImporter.Import(reader), output);
            actualJson = General.Program.StrictUtf8.GetString(output.ToArray());
        }

        using var expected = JsonDocument.Parse(File.ReadAllText(expectedPath, General.Program.StrictUtf8));
        using var actual = JsonDocument.Parse(actualJson);

        var differences = new List<string>();
        Compare(expected.RootElement, actual.RootElement, "$", differences);

        Assert.True(differences.Count == 0,
            $"{name}.csv does not match samples/expected/{name}.json:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", differences)
            + $"{Environment.NewLine}Actual JSON:{Environment.NewLine}{actualJson}");
    }

    private static void Compare(JsonElement expected, JsonElement actual, string path, List<string> differences)
    {
        if (expected.ValueKind != actual.ValueKind)
        {
            differences.Add($"{path}: expected {Describe(expected)} but got {Describe(actual)}");
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                var expectedNames = expected.EnumerateObject().Select(p => p.Name).ToList();
                var actualNames = actual.EnumerateObject().Select(p => p.Name).ToList();
                foreach (var missing in expectedNames.Except(actualNames, StringComparer.Ordinal))
                {
                    differences.Add($"{path}.{missing}: missing in actual output");
                }
                foreach (var extra in actualNames.Except(expectedNames, StringComparer.Ordinal))
                {
                    differences.Add($"{path}.{extra}: unexpected property {Describe(actual.GetProperty(extra))}");
                }
                foreach (var name in expectedNames.Intersect(actualNames, StringComparer.Ordinal))
                {
                    Compare(expected.GetProperty(name), actual.GetProperty(name), $"{path}.{name}", differences);
                }
                break;

            case JsonValueKind.Array:
                var expectedItems = expected.EnumerateArray().ToList();
                var actualItems = actual.EnumerateArray().ToList();
                if (expectedItems.Count != actualItems.Count)
                {
                    differences.Add($"{path}: expected {expectedItems.Count} items but got {actualItems.Count}");
                }
                for (var i = 0; i < Math.Min(expectedItems.Count, actualItems.Count); i++)
                {
                    Compare(expectedItems[i], actualItems[i], $"{path}[{i}]", differences);
                }
                break;

            case JsonValueKind.Number:
                if (expected.GetDecimal() != actual.GetDecimal())
                {
                    differences.Add($"{path}: expected {expected.GetRawText()} but got {actual.GetRawText()}");
                }
                break;

            case JsonValueKind.String:
                if (!string.Equals(expected.GetString(), actual.GetString(), StringComparison.Ordinal))
                {
                    differences.Add($"{path}: expected {expected.GetRawText()} but got {actual.GetRawText()}");
                }
                break;
        }
    }

    private static string Describe(JsonElement element) => $"{element.ValueKind} {element.GetRawText()}";

    private static List<string> FindSamplesWithExpectedJson()
    {
        var samples = Path.Combine(FindRepositoryRoot(), "samples");
        return Directory.EnumerateFiles(samples, "*.csv")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(name => File.Exists(Path.Combine(samples, "expected", name + ".json")))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LeaseImportAssignment.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
