using System.Globalization;

namespace LeaseImport;

internal sealed record ValidationResult(string? LeaseId, Lease? Lease, IReadOnlyList<Problem> Problems);

/// <summary>
/// Validates the values of one data row. Pure: no I/O, no culture dependence, no duplicate tracking.
/// </summary>
internal static class LeaseValidator
{
    public const string LeaseIdColumn = "LeaseId";
    public const string AssetClassColumn = "AssetClass";
    public const string DescriptionColumn = "Description";
    public const string CurrencyColumn = "Currency";
    public const string OpeningColumn = "Opening";
    public const string AdditionsColumn = "Additions";
    public const string DepreciationColumn = "Depreciation";
    public const string DisposalsColumn = "Disposals";

    public static readonly IReadOnlyList<string> RequiredColumns =
    [
        LeaseIdColumn, AssetClassColumn, DescriptionColumn, CurrencyColumn,
        OpeningColumn, AdditionsColumn, DepreciationColumn, DisposalsColumn,
    ];

    private const NumberStyles AmountStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
    private const int MaxDecimals = 2;

    /// <summary>Active ISO-4217 currency codes (circulating currencies, no fund/metal/test codes).</summary>
    internal static readonly HashSet<string> ActiveCurrencyCodes = new(StringComparer.Ordinal)
    {
        "AED", "AFN", "ALL", "AMD", "AOA", "ARS", "AUD", "AWG", "AZN", "BAM", "BBD", "BDT",
        "BHD", "BIF", "BMD", "BND", "BOB", "BRL", "BSD", "BTN", "BWP", "BYN", "BZD", "CAD",
        "CDF", "CHF", "CLP", "CNY", "COP", "CRC", "CUP", "CVE", "CZK", "DJF", "DKK", "DOP",
        "DZD", "EGP", "ERN", "ETB", "EUR", "FJD", "FKP", "GBP", "GEL", "GHS", "GIP", "GMD",
        "GNF", "GTQ", "GYD", "HKD", "HNL", "HTG", "HUF", "IDR", "ILS", "INR", "IQD", "IRR",
        "ISK", "JMD", "JOD", "JPY", "KES", "KGS", "KHR", "KMF", "KPW", "KRW", "KWD", "KYD",
        "KZT", "LAK", "LBP", "LKR", "LRD", "LSL", "LYD", "MAD", "MDL", "MGA", "MKD", "MMK",
        "MNT", "MOP", "MRU", "MUR", "MVR", "MWK", "MXN", "MYR", "MZN", "NAD", "NGN", "NIO",
        "NOK", "NPR", "NZD", "OMR", "PAB", "PEN", "PGK", "PHP", "PKR", "PLN", "PYG", "QAR",
        "RON", "RSD", "RUB", "RWF", "SAR", "SBD", "SCR", "SDG", "SEK", "SGD", "SHP", "SLE",
        "SOS", "SRD", "SSP", "STN", "SVC", "SYP", "SZL", "THB", "TJS", "TMT", "TND", "TOP",
        "TRY", "TTD", "TWD", "TZS", "UAH", "UGX", "USD", "UYU", "UZS", "VED", "VES", "VND",
        "VUV", "WST", "XAF", "XCD", "XCG", "XOF", "XPF", "YER", "ZAR", "ZMW", "ZWG",
    };

    /// <param name="line">Physical line the record starts on.</param>
    /// <param name="values">Raw (untrimmed) values by column name. A missing column counts as empty.</param>
    public static ValidationResult Validate(int line, IReadOnlyDictionary<string, string> values)
    {
        string Value(string column) => values.TryGetValue(column, out var raw) ? raw.Trim() : string.Empty;

        var leaseId = Value(LeaseIdColumn);
        string? problemLeaseId = leaseId.Length == 0 ? null : leaseId;
        var problems = new List<Problem>();

        void Report(string field, string code, string message) =>
            problems.Add(new Problem(line, problemLeaseId, field, code, message));

        void Require(string column, string value)
        {
            if (value.Length == 0)
            {
                Report(column, ProblemCodes.MissingValue, ProblemMessages.MissingValue(column));
            }
        }

        decimal Amount(string column)
        {
            var text = Value(column);
            if (text.Length == 0)
            {
                Report(column, ProblemCodes.MissingValue, ProblemMessages.MissingValue(column));
                return 0m;
            }
            if (!decimal.TryParse(text, AmountStyles, CultureInfo.InvariantCulture, out var amount))
            {
                Report(column, ProblemCodes.InvalidNumber, ProblemMessages.InvalidNumber(column, text));
                return 0m;
            }
            if (amount.Scale > MaxDecimals)
            {
                Report(column, ProblemCodes.TooManyDecimals, ProblemMessages.TooManyDecimals(column, text));
            }
            if (amount < 0m)
            {
                Report(column, ProblemCodes.NegativeAmount, ProblemMessages.NegativeAmount(column, text));
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
            Report(CurrencyColumn, ProblemCodes.InvalidCurrency, ProblemMessages.InvalidCurrency(currency));
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
