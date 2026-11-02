using System.Reflection;
using System.Text.Json;

namespace LeaseImport.Tests;

/// <summary>Light CLI tests. Main is private, so it is invoked by reflection (no production change).</summary>
[Collection("Culture")]
public sealed class ProgramTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "LeaseImportTests-" + Guid.NewGuid().ToString("N"));

    public ProgramTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static (int ExitCode, string StdErr) Run(params string[] args)
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
}
