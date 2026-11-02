using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace LeaseImport.Common;

/// <summary>
/// Every constant and static readonly lookup of the project, grouped by the class that uses it.
/// </summary>
internal static class General
{
    #region CsvParser

    internal static class CsvParser
    {
        public const char Quote = '"';
        public const char Separator = ',';
        public const char Bom = '﻿';
    }

    #endregion

    #region LeaseValidator

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

        public const NumberStyles AmountStyles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        public const int MaxDecimals = 2;

        /// <summary>Strict amount format: optional sign, no leading zeros, digits on both sides of a '.'.</summary>
        public static readonly Regex AmountFormat = new(
            @"^[+-]?(0|[1-9][0-9]*)(\.[0-9]+)?$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>Active ISO-4217 currency codes (circulating currencies, no fund/metal/test codes).</summary>
        public static readonly HashSet<string> ActiveCurrencyCodes = new(StringComparer.Ordinal)
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
    }

    #endregion

    #region LeaseImporter

    internal static class LeaseImporter
    {
        public const int HeaderLine = 1;

        public static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
    }

    #endregion

    #region ProblemMessages

    internal static class ProblemMessages
    {
        public const string EmptyFile = "EmptyFile";
        public const string MissingColumn = "MissingColumn";
        public const string DuplicateColumn = "DuplicateColumn";
        public const string UnclosedQuote = "UnclosedQuote";
        public const string InvalidFieldCount = "InvalidFieldCount";
        public const string MissingValue = "MissingValue";
        public const string InvalidNumber = "InvalidNumber";
        public const string TooManyDecimals = "TooManyDecimals";
        public const string NegativeAmount = "NegativeAmount";
        public const string InvalidCurrency = "InvalidCurrency";
        public const string DuplicateLeaseId = "DuplicateLeaseId";
    }

    #endregion

    #region Program

    internal static class Program
    {
        public const int Success = 0;
        public const int UsageError = 1;
        public const int InputError = 2;
        public const int OutputError = 3;

        public static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
        public static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    }

    #endregion
}
