namespace LeaseImport.Tests.Tests;

/// <summary>
/// The JScript tests use JSON fixtures produced by the C# importer. These tests keep the
/// committed fixtures in sync with what the importer writes today (C# -> JScript contract).
/// </summary>
public class JscriptFixtureTests
{
    [Theory]
    [InlineData("leases.csv", "leases.json")]
    [InlineData("leases_with_problems.csv", "leases_with_problems.json")]
    public void Importer_output_equals_the_committed_JScript_fixture(string csvName, string fixtureName)
    {
        var root = FindRepositoryRoot();
        var csvPath = Path.Combine(root, "samples", csvName);
        var fixturePath = Path.Combine(root, "jscript", "tests", "fixtures", fixtureName);

        using var reader = new StreamReader(csvPath, new System.Text.UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
        var actual = TestData.ToJson(LeaseImporter.Import(reader));
        var expected = File.ReadAllText(fixturePath);

        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    // The CLI appends a newline, and line endings follow Environment.NewLine (deferred item m4).
    private static string Normalize(string json) => json.Replace("\r\n", "\n").TrimEnd('\n');

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
