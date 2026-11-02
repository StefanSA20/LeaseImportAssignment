using System.Text;
using System.Text.Json;
using LeaseImport.Common;

namespace LeaseImport.Tests.Tests;

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
        "leases_with_problems",
        "leases-excel-sep-hint", "leases-trailing-separators-rows", "leases-trailing-separators-all",
        "leases-duplicate-column", "leases-extra-columns", "leases-reordered-columns", "leases-header-only",
        "leases-id-case-whitespace",
    ];

    /// <summary>Samples that are not valid UTF-8, so the CLI must refuse them (exit 2, no JSON).</summary>
    internal static readonly string[] UnreadableSamples =
    [
        "leases-excel-ansi", "leases-excel-utf16",
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

    [Fact]
    public void All_samples_are_found()
    {
        Assert.Equal(HandWrittenSamples.Length, FindSamplesWithExpectedJson().Count);
        Assert.Equal(16, HandWrittenSamples.Length);
    }

    [Fact]
    public void Every_sample_csv_has_expected_json_or_is_listed_as_unreadable()
    {
        var withJson = FindSamplesWithExpectedJson();
        var samples = Path.Combine(FindRepositoryRoot(), "samples");
        var unaccounted = Directory.EnumerateFiles(samples, "*.csv")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(name => !withJson.Contains(name) && !UnreadableSamples.Contains(name))
            .ToList();

        Assert.True(unaccounted.Count == 0,
            "Every samples/*.csv needs samples/expected/<name>.json or an entry in UnreadableSamples: "
            + string.Join(", ", unaccounted));
        foreach (var name in UnreadableSamples)
        {
            Assert.True(File.Exists(Path.Combine(samples, name + ".csv")), $"samples/{name}.csv is missing");
            Assert.DoesNotContain(name, withJson);
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

    internal static List<string> FindSamplesWithExpectedJson()
    {
        var samples = Path.Combine(FindRepositoryRoot(), "samples");
        return Directory.EnumerateFiles(samples, "*.csv")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(name => File.Exists(Path.Combine(samples, "expected", name + ".json")))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
    }

    internal static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "LeaseImportAssignment.slnx")))
        {
            directory = directory.Parent;
        }
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}

/// <summary>
/// Files Danish users really produce from Excel that are not UTF-8 (cp1252/ANSI, UTF-16 LE "Unicode text").
/// Decoding is strict (CLAUDE.md section 5), so the CLI exits with 2, writes a message to stderr and creates no JSON.
/// Runs in the "Culture" collection because it redirects Console.Error, like ProgramTests.
/// </summary>
[Collection("Culture")]
public sealed class UnreadableSampleFilesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "LeaseImportTests-" + Guid.NewGuid().ToString("N"));

    public UnreadableSampleFilesTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    public static TheoryData<string> UnreadableSamples()
    {
        var data = new TheoryData<string>();
        foreach (var name in SampleFilesIntegrationTests.UnreadableSamples)
        {
            data.Add(name);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(UnreadableSamples))]
    public void Non_utf8_sample_gives_exit_code_2_and_stderr_mentioning_utf8_and_no_output(string name)
    {
        var input = Path.Combine(SampleFilesIntegrationTests.FindRepositoryRoot(), "samples", name + ".csv");
        var output = Path.Combine(_dir, name + ".json");

        var (exitCode, stdErr) = ProgramTests.Run(input, output);

        Assert.Equal(2, exitCode);
        Assert.Contains("UTF-8", stdErr, StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData("leases-excel-ansi", new byte[] { 0x4C, 0x65 })]
    [InlineData("leases-excel-utf16", new byte[] { 0xFF, 0xFE, 0x4C, 0x00 })]
    public void Unreadable_sample_has_the_intended_encoding(string name, byte[] expectedStart)
    {
        var bytes = File.ReadAllBytes(Path.Combine(SampleFilesIntegrationTests.FindRepositoryRoot(), "samples", name + ".csv"));

        Assert.Equal(expectedStart, bytes.Take(expectedStart.Length).ToArray());
        Assert.Throws<DecoderFallbackException>(() => General.Program.StrictUtf8.GetString(bytes));
    }
}
