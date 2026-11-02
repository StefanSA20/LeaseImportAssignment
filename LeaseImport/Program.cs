using System.Text;

namespace LeaseImport;

internal static class Program
{
    private const int Success = 0;
    private const int UsageError = 1;
    private const int InputError = 2;
    private const int OutputError = 3;

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    private static int Main(string[] args)
    {
        if (args.Length is < 1 or > 2)
        {
            Console.Error.WriteLine("Usage: LeaseImport <input.csv> [output.json]");
            return UsageError;
        }

        var inputPath = args[0];
        string json;
        try
        {
            using var reader = new StreamReader(inputPath, Utf8NoBom, detectEncodingFromByteOrderMarks: true);
            json = LeaseImporter.ToJson(LeaseImporter.Import(reader));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"Cannot read input file '{inputPath}': {ex.Message}");
            return InputError;
        }

        if (args.Length == 1)
        {
            Console.OutputEncoding = Utf8NoBom;
            Console.Out.WriteLine(json);
            return Success;
        }

        var outputPath = args[1];
        try
        {
            File.WriteAllText(outputPath, json + Environment.NewLine, Utf8NoBom);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            Console.Error.WriteLine($"Cannot write output file '{outputPath}': {ex.Message}");
            return OutputError;
        }
        return Success;
    }
}
