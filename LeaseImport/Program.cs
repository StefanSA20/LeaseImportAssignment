using System.Text;
using LeaseImport.Common;
using static LeaseImport.Common.General.Program;

namespace LeaseImport;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            Console.Error.WriteLine("Usage: LeaseImport <input.csv> [output.json]");
            return UsageError;
        }

        var inputPath = args[0];
        ImportResult result;
        try
        {
            using var reader = new StreamReader(inputPath, StrictUtf8, detectEncodingFromByteOrderMarks: false);
            result = LeaseImporter.Import(reader);
        }
        // NOTE: strict UTF-8 decision — to be double-checked by the user.
        catch (DecoderFallbackException ex)
        {
            Console.Error.WriteLine($"The input file '{inputPath}' is not valid UTF-8 and can't be read: {ex.Message}");
            return InputError;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"Cannot read input file '{inputPath}': {ex.Message}");
            return InputError;
        }

        if (args.Length == 1)
        {
            using var stdout = Console.OpenStandardOutput();
            WriteJsonLine(result, stdout);
            return Success;
        }

        var outputPath = args[1];
        try
        {
            using var output = File.Create(outputPath);
            WriteJsonLine(result, output);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"Cannot write output file '{outputPath}': {ex.Message}");
            return OutputError;
        }
        return Success;
    }

    private static void WriteJsonLine(ImportResult result, Stream output)
    {
        LeaseImporter.WriteJson(result, output);
        output.Write(Utf8NoBom.GetBytes(Environment.NewLine));
    }
}
