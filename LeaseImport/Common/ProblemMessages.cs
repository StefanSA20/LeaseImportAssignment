using System.Globalization;

namespace LeaseImport.Common;

internal static class ProblemMessages
{
    public static string EmptyFile() =>
        "The file can't be processed because it is empty (no header line was found).";

    public static string MissingColumn(string field) =>
        $"The file can't be processed because the required column '{field}' is missing from the header.";

    public static string DuplicateColumn(string field) =>
        $"The file can't be processed because the column '{field}' appears more than once in the header.";

    public static string UnclosedQuote(int line) =>
        string.Create(CultureInfo.InvariantCulture,
            $"This lease can't be processed because a quoted value starting on line {line} is never closed.");

    public static string InvalidFieldCount(int actual, int expected) =>
        string.Create(CultureInfo.InvariantCulture,
            $"This lease can't be processed because it has {actual} values, but the header has {expected} columns.");

    public static string MissingValue(string field) =>
        $"This lease can't be processed because '{field}' is empty.";

    public static string InvalidNumber(string field, string value) =>
        $"This lease can't be processed because '{field}' has the value '{value}', which is not a valid amount. " +
        "Use digits with '.' as the decimal point and no thousands separators, e.g. 1250000.50.";

    public static string TooManyDecimals(string field, string value) =>
        $"This lease can't be processed because '{field}' has the value '{value}', which has more than 2 decimals.";

    public static string NegativeAmount(string field, string value) =>
        $"This lease can't be processed because '{field}' has the value '{value}', which is negative. Amounts must be 0 or more.";

    public static string InvalidCurrency(string value) =>
        $"This lease can't be processed because '{value}' is not a valid ISO-4217 currency code (use three uppercase letters, e.g. DKK).";

    public static string DuplicateLeaseId(string id, int firstLine) =>
        string.Create(CultureInfo.InvariantCulture,
            $"This lease can't be processed because its ID '{id}' is not unique (it already appears on line {firstLine}).");
}
