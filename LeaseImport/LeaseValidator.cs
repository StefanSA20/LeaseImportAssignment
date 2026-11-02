using System.Globalization;
using LeaseImport.Common;
using static LeaseImport.Common.General.LeaseValidator;

namespace LeaseImport;

internal sealed record ValidationResult(string? LeaseId, Lease? Lease, IReadOnlyList<Problem> Problems);

/// <summary>
/// Validates the values of one data row. Pure: no I/O, no culture dependence, no duplicate tracking.
/// </summary>
internal static class LeaseValidator
{
    /// <param name="line">Physical line the record starts on.</param>
    /// <param name="fields">Raw (untrimmed) values of the record.</param>
    /// <param name="columnIndex">Position of each column in <paramref name="fields"/>, built once from the header. A missing column counts as empty.</param>
    public static ValidationResult Validate(int line, IReadOnlyList<string> fields, IReadOnlyDictionary<string, int> columnIndex)
    {
        string Value(string column) =>
            columnIndex.TryGetValue(column, out var index) ? fields[index].Trim() : string.Empty;

        var leaseId = Value(LeaseIdColumn);
        string? problemLeaseId = leaseId.Length == 0 ? null : leaseId;
        var problems = new List<Problem>();

        void Report(string field, string code, string message) =>
            problems.Add(new Problem(line, problemLeaseId, field, code, message));

        void Require(string column, string value)
        {
            if (value.Length == 0)
            {
                Report(column, General.ProblemMessages.MissingValue, ProblemMessages.MissingValue(column));
            }
        }

        decimal Amount(string column)
        {
            var text = Value(column);
            if (text.Length == 0)
            {
                Report(column, General.ProblemMessages.MissingValue, ProblemMessages.MissingValue(column));
                return 0m;
            }
            if (!AmountFormat.IsMatch(text)
                || !decimal.TryParse(text, AmountStyles, CultureInfo.InvariantCulture, out var amount)
                || (amount == 0m && text[0] == '-'))
            {
                Report(column, General.ProblemMessages.InvalidNumber, ProblemMessages.InvalidNumber(column, text));
                return 0m;
            }
            if (amount.Scale > MaxDecimals)
            {
                Report(column, General.ProblemMessages.TooManyDecimals, ProblemMessages.TooManyDecimals(column, text));
            }
            if (amount < 0m)
            {
                Report(column, General.ProblemMessages.NegativeAmount, ProblemMessages.NegativeAmount(column, text));
            }
            return amount;
        }

        Require(LeaseIdColumn, leaseId);

        var assetClass = Value(AssetClassColumn);
        Require(AssetClassColumn, assetClass);

        var description = Value(DescriptionColumn);

        var currency = Value(CurrencyColumn);
        if (currency.Length == 0)
        {
            Require(CurrencyColumn, currency);
        }
        else if (!ActiveCurrencyCodes.Contains(currency))
        {
            Report(CurrencyColumn, General.ProblemMessages.InvalidCurrency, ProblemMessages.InvalidCurrency(currency));
        }

        var opening = Amount(OpeningColumn);
        var additions = Amount(AdditionsColumn);
        var depreciation = Amount(DepreciationColumn);
        var disposals = Amount(DisposalsColumn);

        if (problems.Count > 0)
        {
            return new ValidationResult(problemLeaseId, null, problems);
        }

        var lease = new Lease(line, leaseId, assetClass, description, currency,
            opening, additions, depreciation, disposals);
        return new ValidationResult(problemLeaseId, lease, problems);
    }
}
