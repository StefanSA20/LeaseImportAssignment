using System.Text;
using System.Text.Json;
using LeaseImport.Common;

namespace LeaseImport.Tests.Tests;

/// <summary>
/// samples/leases-unusual-text.csv (UTF-8 with BOM, CRLF): text with escaped quotes, emoji and other 4-byte
/// UTF-8 characters (surrogate pairs in .NET strings), Danish letters and a combining accent must reach the
/// JSON unchanged. The serializer may write them raw or as \uXXXX escapes; decoding must give the same string.
/// Runs in the "Culture" collection because the CLI test redirects Console.Error, like ProgramTests.
/// </summary>
[Collection("Culture")]
public sealed class UnicodeTextRoundTripTests : IDisposable
{
    private const string OfficeBuilding = "🏢"; // U+1F3E2
    private const string Car = "🚗"; // U+1F697
    private const string DoubleStruckA = "𝔸"; // U+1D538
    private const string CjkIdeograph = "𠜎"; // U+2070E
    private const string CombiningAcute = "́";

    private static readonly string[] ExpectedDescriptions =
    [
        "Office \"Nord\" Aarhus",
        "Company car " + Car + " Odense",
        "Math " + DoubleStruckA + " and rare " + CjkIdeograph + " glyphs",
        "Lager Ærøskøbing Øst Åbyhøj æøå ÆØÅ",
        "Vesterbro Cafe" + CombiningAcute,
        OfficeBuilding + " HQ, \"Tower A\" " + OfficeBuilding,
        "Annex",
    ];

    private static readonly string[] ExpectedAssetClasses =
    [
        "Property", "Vehicles", "Equipment", "Property", "Equipment", "Property", OfficeBuilding + " Buildings",
    ];

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "LeaseImportTests-" + Guid.NewGuid().ToString("N"));

    public UnicodeTextRoundTripTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static string SamplePath =>
        Path.Combine(SampleFilesIntegrationTests.FindRepositoryRoot(), "samples", "leases-unusual-text.csv");

    private static ImportResult ImportSampleFile()
    {
        using var reader = new StreamReader(SamplePath, General.Program.StrictUtf8, detectEncodingFromByteOrderMarks: false);
        return LeaseImporter.Import(reader);
    }

    [Fact]
    public void Sample_file_is_utf8_with_bom_and_crlf_and_holds_the_4_byte_sequences()
    {
        var bytes = File.ReadAllBytes(SamplePath);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        Assert.Equal(new byte[] { 0x0D, 0x0A }, bytes.Skip(bytes.Length - 2).ToArray());
        var text = General.Program.StrictUtf8.GetString(bytes, 3, bytes.Length - 3);
        Assert.DoesNotContain("\n", text.Replace("\r\n", string.Empty), StringComparison.Ordinal);
        Assert.True(ContainsBytes(bytes, [0xF0, 0x9F, 0x8F, 0xA2]), "U+1F3E2 missing");
        Assert.True(ContainsBytes(bytes, [0xF0, 0x9F, 0x9A, 0x97]), "U+1F697 missing");
        Assert.True(ContainsBytes(bytes, [0xF0, 0x9D, 0x94, 0xB8]), "U+1D538 missing");
        Assert.True(ContainsBytes(bytes, [0xF0, 0xA0, 0x9C, 0x8E]), "U+2070E missing");
        Assert.True(ContainsBytes(bytes, [0x65, 0xCC, 0x81]), "e + U+0301 missing");
    }

    [Fact]
    public void Importer_reads_every_text_value_exactly()
    {
        var result = ImportSampleFile();

        Assert.Equal(ExpectedDescriptions, result.Leases.Select(l => l.Description).ToArray());
        Assert.Equal(ExpectedAssetClasses, result.Leases.Select(l => l.AssetClass).ToArray());
        Assert.All(result.Leases, l => Assert.Equal("DKK", l.Currency));
        Assert.Equal(15, result.Leases[4].Description.Length); // combining accent kept, not normalized to U+00E9

        ModelAssert.AssertProblems(
            [
                new Problem
                {
                    Line = 9, LeaseId = "L-7008", Field = "Currency", Code = "InvalidCurrency",
                    Message = ProblemMessages.InvalidCurrency("D" + OfficeBuilding + "K"),
                },
            ],
            result.Problems);
        Assert.Equal(
            "This lease can't be processed because 'D" + OfficeBuilding
            + "K' is not a valid ISO-4217 currency code (use three uppercase letters, e.g. DKK).",
            result.Problems[0].Message);
    }

    [Fact]
    public void WriteJson_output_is_valid_utf8_and_every_string_round_trips()
    {
        var result = ImportSampleFile();
        using var output = new MemoryStream();
        LeaseImporter.WriteJson(result, output);

        AssertJsonRoundTrips(result, output.ToArray());
    }

    [Fact]
    public void Cli_output_file_is_valid_utf8_and_every_string_round_trips()
    {
        var outputPath = Path.Combine(_dir, "unusual.json");

        var (exitCode, stdErr) = ProgramTests.Run(SamplePath, outputPath);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stdErr);
        AssertJsonRoundTrips(ImportSampleFile(), File.ReadAllBytes(outputPath));
    }

    private static void AssertJsonRoundTrips(ImportResult result, byte[] jsonBytes)
    {
        var jsonText = General.Program.StrictUtf8.GetString(jsonBytes); // throws on invalid UTF-8
        Assert.True(IsWellFormedUtf16(jsonText), "the JSON text contains an unpaired surrogate");

        using var document = JsonDocument.Parse(jsonBytes);
        var leases = document.RootElement.GetProperty("Leases").EnumerateArray().ToList();
        var problems = document.RootElement.GetProperty("Problems").EnumerateArray().ToList();
        Assert.Equal(result.Leases.Count, leases.Count);
        Assert.Equal(result.Problems.Count, problems.Count);
        Assert.Equal(7, leases.Count);
        Assert.Single(problems);

        for (var i = 0; i < leases.Count; i++)
        {
            AssertStringProperty(result.Leases[i].LeaseId, leases[i], "LeaseId");
            AssertStringProperty(result.Leases[i].AssetClass, leases[i], "AssetClass");
            AssertStringProperty(result.Leases[i].Description, leases[i], "Description");
            AssertStringProperty(result.Leases[i].Currency, leases[i], "Currency");
            AssertStringProperty(ExpectedDescriptions[i], leases[i], "Description");
            AssertStringProperty(ExpectedAssetClasses[i], leases[i], "AssetClass");
        }

        for (var i = 0; i < problems.Count; i++)
        {
            AssertStringProperty(result.Problems[i].LeaseId, problems[i], "LeaseId");
            AssertStringProperty(result.Problems[i].Field, problems[i], "Field");
            AssertStringProperty(result.Problems[i].Code, problems[i], "Code");
            AssertStringProperty(result.Problems[i].Message, problems[i], "Message");
        }
        AssertStringProperty(ProblemMessages.InvalidCurrency("D" + OfficeBuilding + "K"), problems[0], "Message");
    }

    private static void AssertStringProperty(string? expected, JsonElement element, string name)
    {
        var actual = element.GetProperty(name).GetString();
        Assert.True(string.Equals(expected, actual, StringComparison.Ordinal),
            $"{name}: expected {Escape(expected)} but got {Escape(actual)}");
        if (actual is not null)
        {
            Assert.True(IsWellFormedUtf16(actual), $"{name} has an unpaired surrogate: {Escape(actual)}");
        }
    }

    private static bool IsWellFormedUtf16(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return false;
                }
                i++;
            }
            else if (char.IsLowSurrogate(text[i]))
            {
                return false;
            }
        }
        return true;
    }

    private static string Escape(string? text) =>
        text is null ? "null" : string.Concat(text.Select(c => c < 0x80 ? c.ToString() : $"\\u{(int)c:X4}"));

    private static bool ContainsBytes(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i + needle.Length <= haystack.Length; i++)
        {
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
            {
                return true;
            }
        }
        return false;
    }
}
