using System.Reflection;
using System.Text.Json;

namespace LeaseImport.Tests.Tests;

/// <summary>Light CLI tests. Main is private, so it is invoked by reflection (no production change).</summary>
[Collection("Culture")]
public sealed class ProgramTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "LeaseImportTests-" + Guid.NewGuid().ToString("N"));

    public ProgramTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    public static (int ExitCode, string StdErr) Run(params string[] args)
    {
        var main = typeof(LeaseImporter).Assembly.GetType("LeaseImport.Program")!
            .GetMethod("Main", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        var originalError = Console.Error;
        var error = new StringWriter();
        Console.SetError(error);
        try
        {
            var exitCode = (int)main.Invoke(null, [args])!;
            return (exitCode, error.ToString());
        }
        finally
        {
            Console.SetError(originalError);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Wrong_argument_count_is_usage_error_1(int argumentCount)
    {
        var (exitCode, stdErr) = Run(Enumerable.Range(0, argumentCount).Select(i => $"arg{i}").ToArray());

        Assert.Equal(1, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stdErr));
    }

    [Fact]
    public void Missing_input_file_gives_exit_code_2_and_stderr_message_and_no_output()
    {
        var output = Path.Combine(_dir, "out.json");

        var (exitCode, stdErr) = Run(Path.Combine(_dir, "does-not-exist.csv"), output);

        Assert.Equal(2, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stdErr));
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Unwritable_output_gives_exit_code_3()
    {
        var output = Path.Combine(_dir, "no-such-dir", "out.json");

        var (exitCode, stdErr) = Run(TestData.SamplePath, output);

        Assert.Equal(3, exitCode);
        Assert.False(string.IsNullOrWhiteSpace(stdErr));
    }

    [Fact]
    public void Sample_is_written_as_json_with_exit_code_0()
    {
        var output = Path.Combine(_dir, "out.json");

        var (exitCode, _) = Run(TestData.SamplePath, output);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal(4, doc.RootElement.GetProperty("Leases").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("Problems").GetArrayLength());
    }

    [Fact]
    public void Json_with_problems_still_exits_0()
    {
        var input = Path.Combine(_dir, "in.csv");
        File.WriteAllText(input, TestData.Header + "\nL-1,Property,Office,dkk,1,0,0,0\n");
        var output = Path.Combine(_dir, "out.json");

        var (exitCode, _) = Run(input, output);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(File.ReadAllText(output));
        Assert.Equal("InvalidCurrency", doc.RootElement.GetProperty("Problems")[0].GetProperty("Code").GetString());
    }

    [Fact]
    public void Output_file_is_utf8_without_bom()
    {
        var output = Path.Combine(_dir, "out.json");

        Run(TestData.SamplePath, output);

        var bytes = File.ReadAllBytes(output);
        Assert.NotEqual(0xEF, bytes[0]);
    }

    // ---------- strict UTF-8 (CLAUDE.md section 5) ----------

    [Fact]
    public void Cp1252_input_gives_exit_code_2_stderr_mentioning_utf8_and_no_output()
    {
        var input = Path.Combine(_dir, "ansi.csv");
        // "Kontor Århus" / "København" as Windows-1252: 0xC5 = 'Å', 0xF8 = 'ø'. Both are invalid UTF-8 here.
        var bytes = new List<byte>();
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes(TestData.Header + "\r\nL-1,Property,Kontor K"));
        bytes.Add(0xF8);
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("benhavn,DKK,100.00,0,10.00,0\r\n"));
        File.WriteAllBytes(input, bytes.ToArray());
        var output = Path.Combine(_dir, "out.json");

        var (exitCode, stdErr) = Run(input, output);

        Assert.Equal(2, exitCode);
        Assert.Contains("UTF-8", stdErr);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Invalid_utf8_late_in_the_file_still_gives_exit_code_2_and_no_output()
    {
        var input = Path.Combine(_dir, "late.csv");
        var text = new System.Text.StringBuilder(TestData.Header + "\n");
        for (var i = 0; i < 5000; i++)
        {
            text.Append($"L-{i},Property,Office,DKK,1,0,0,0\n");
        }
        var bytes = new List<byte>(System.Text.Encoding.ASCII.GetBytes(text.ToString()));
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("L-x,Property,K"));
        bytes.Add(0xF8);
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("benhavn,DKK,1,0,0,0\n"));
        File.WriteAllBytes(input, bytes.ToArray());
        var output = Path.Combine(_dir, "out.json");

        var (exitCode, stdErr) = Run(input, output);

        Assert.Equal(2, exitCode);
        Assert.Contains("UTF-8", stdErr);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public void Invalid_utf8_after_a_fatal_header_still_gives_exit_code_2()
    {
        var input = Path.Combine(_dir, "badheader.csv");
        var bytes = new List<byte>(System.Text.Encoding.ASCII.GetBytes("LeaseId,AssetClass\nL-1,K"));
        bytes.Add(0xF8);
        bytes.AddRange(System.Text.Encoding.ASCII.GetBytes("b\n"));
        File.WriteAllBytes(input, bytes.ToArray());
        var output = Path.Combine(_dir, "out.json");

        var (exitCode, stdErr) = Run(input, output);

        Assert.Equal(2, exitCode);
        Assert.Contains("UTF-8", stdErr);
        Assert.False(File.Exists(output));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Valid_utf8_with_danish_letters_is_imported(bool withBom)
    {
        var input = Path.Combine(_dir, "utf8.csv");
        var text = TestData.Header + "\r\nL-1,Property,Kontor København Ærø,DKK,100.00,0,10.00,0\r\n";
        File.WriteAllBytes(input, new System.Text.UTF8Encoding(withBom).GetPreamble().Concat(new System.Text.UTF8Encoding(false).GetBytes(text)).ToArray());
        var output = Path.Combine(_dir, "out.json");

        var (exitCode, _) = Run(input, output);

        Assert.Equal(0, exitCode);
        using var doc = JsonDocument.Parse(File.ReadAllText(output));
        var lease = Assert.Single(doc.RootElement.GetProperty("Leases").EnumerateArray());
        Assert.Equal("L-1", lease.GetProperty("LeaseId").GetString());
        Assert.Equal("Kontor København Ærø", lease.GetProperty("Description").GetString());
        Assert.Equal(0, doc.RootElement.GetProperty("Problems").GetArrayLength());
    }
}
