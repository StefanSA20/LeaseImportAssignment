using System.Globalization;
using LeaseImport.Common;
using static LeaseImport.Common.General.LeaseValidator;

namespace LeaseImport;

public class ValidationResult
{
    public string? LeaseId { get; set; }
    public Lease? Lease { get; set; }
    public List<Problem> Problems { get; set; } = [];
}

/// <summary>
/// Validates the values of one data row. Pure: no I/O, no culture dependence, no duplicate tracking.
/// </summary>
public static class LeaseValidator
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
            problems.Add(new Problem { Line = line, LeaseId = problemLeaseId, Field = field, Code = code, Message = message });

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
            return new ValidationResult { LeaseId = problemLeaseId, Lease = null, Problems = problems };
        }

        var lease = new Lease
        {
            Line = line,
            LeaseId = leaseId,
            AssetClass = assetClass,
            Description = description,
            Currency = currency,
            Opening = opening,
            Additions = additions,
            Depreciation = depreciation,
            Disposals = disposals,
        };
        return new ValidationResult { LeaseId = problemLeaseId, Lease = lease, Problems = problems };
    }
}
