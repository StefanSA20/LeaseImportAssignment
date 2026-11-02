using LeaseImport.Common;

namespace LeaseImport.Tests;

public class LeaseValidatorTests
{
    private const int Line = 7;

    internal static Dictionary<string, string> ValidRow(params (string Column, string Value)[] overrides)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["LeaseId"] = "L-1",
            ["AssetClass"] = "Property",
            ["Description"] = "Office Aarhus",
            ["Currency"] = "DKK",
            ["Opening"] = "1250000.50",
            ["Additions"] = "0",
            ["Depreciation"] = "125000.05",
            ["Disposals"] = "0",
        };
        foreach (var (column, value) in overrides)
        {
            values[column] = value;
        }
        return values;
    }

    internal static ValidationResult Validate(params (string Column, string Value)[] overrides) =>
        ValidateRow(Line, ValidRow(overrides));

    /// <summary>Calls the validator with the row's values as fields and the column positions taken from its keys.</summary>
    internal static ValidationResult ValidateRow(int line, Dictionary<string, string> row) =>
        LeaseValidator.Validate(line, row.Values.ToArray(),
            row.Keys.Select((column, index) => (column, index)).ToDictionary(c => c.column, c => c.index, StringComparer.Ordinal));

    internal static Problem SingleProblem(ValidationResult result)
    {
        Assert.Null(result.Lease);
        return Assert.Single(result.Problems);
    }

    public static readonly string[] AmountColumns = ["Opening", "Additions", "Depreciation", "Disposals"];

    // ---------- valid rows ----------

    [Fact]
    public void Valid_row_gives_lease_with_trimmed_values_and_no_problems()
    {
        var result = ValidateRow(2, ValidRow(
            ("LeaseId", "  L-2001 "), ("AssetClass", " Property "), ("Description", "  Office Aarhus  "),
            ("Currency", " DKK "), ("Opening", " 1250000.50 "), ("Additions", "0"),
            ("Depreciation", "\t125000.05\t"), ("Disposals", "0")));

        Assert.Empty(result.Problems);
        Assert.Equal("L-2001", result.LeaseId);
        var lease = Assert.IsType<Lease>(result.Lease);
        Assert.Equal(new Lease(2, "L-2001", "Property", "Office Aarhus", "DKK", 1250000.50m, 0m, 125000.05m, 0m), lease);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_description_is_accepted(string description)
    {
        var result = Validate(("Description", description));

        Assert.Empty(result.Problems);
        Assert.Equal("", result.Lease!.Description);
    }

    [Theory]
    [InlineData("Property")]
    [InlineData("Something Nobody Planned")]
    [InlineData("vehicles")]
    public void Any_non_empty_asset_class_is_accepted(string assetClass)
    {
        var result = Validate(("AssetClass", assetClass));

        Assert.Empty(result.Problems);
        Assert.Equal(assetClass, result.Lease!.AssetClass);
    }

    [Theory]
    [InlineData("1250000.50", "1250000.50")]
    [InlineData("0", "0")]
    [InlineData("0.00", "0.00")]
    [InlineData("125000.05", "125000.05")]
    [InlineData("480000.00", "480000.00")]
    [InlineData("12.5", "12.5")]
    [InlineData("+500.00", "500.00")]
    [InlineData("  42  ", "42")]
    [InlineData("0.50", "0.50")]
    [InlineData("+0", "0")]
    [InlineData("99999999999999.99", "99999999999999.99")]
    public void Valid_amounts_are_parsed_exactly(string text, string expected)
    {
        foreach (var column in AmountColumns)
        {
            var result = Validate((column, text));

            Assert.Empty(result.Problems);
            var lease = result.Lease!;
            var actual = column switch
            {
                "Opening" => lease.Opening,
                "Additions" => lease.Additions,
                "Depreciation" => lease.Depreciation,
                _ => lease.Disposals,
            };
            Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), actual);
        }
    }

    [Theory]
    [InlineData("DKK")]
    [InlineData("EUR")]
    [InlineData("USD")]
    [InlineData("SEK")]
    [InlineData("NOK")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    public void Non_dkk_active_currencies_are_accepted(string currency)
    {
        var result = Validate(("Currency", currency));

        Assert.Empty(result.Problems);
        Assert.Equal(currency, result.Lease!.Currency);
    }

    // ---------- MissingValue ----------

    [Theory]
    [InlineData("LeaseId")]
    [InlineData("AssetClass")]
    [InlineData("Currency")]
    [InlineData("Opening")]
    [InlineData("Additions")]
    [InlineData("Depreciation")]
    [InlineData("Disposals")]
    public void Empty_required_field_gives_MissingValue_with_exact_message(string column)
    {
        foreach (var empty in new[] { "", "   ", "\t" })
        {
            var problem = SingleProblem(Validate((column, empty)));

            Assert.Equal("MissingValue", problem.Code);
            Assert.Equal(column, problem.Field);
            Assert.Equal(Line, problem.Line);
            Assert.Equal($"This lease can't be processed because '{column}' is empty.", problem.Message);
        }
    }

    [Fact]
    public void Missing_LeaseId_gives_null_LeaseId_on_problems()
    {
        var result = Validate(("LeaseId", "  "), ("Currency", "XYZ"));

        Assert.Null(result.LeaseId);
        Assert.Equal(2, result.Problems.Count);
        Assert.All(result.Problems, p => Assert.Null(p.LeaseId));
    }

    [Fact]
    public void Problems_carry_the_trimmed_LeaseId()
    {
        var problem = SingleProblem(Validate(("LeaseId", "  L-9  "), ("Currency", "XYZ")));

        Assert.Equal("L-9", problem.LeaseId);
    }

    [Fact]
    public void Empty_amount_is_never_treated_as_zero()
    {
        var result = Validate(("Additions", ""));

        Assert.Null(result.Lease);
        Assert.Equal("MissingValue", Assert.Single(result.Problems).Code);
    }

    // ---------- InvalidNumber ----------

    [Theory]
    [InlineData("abc")]
    [InlineData("1.250.000,50")]
    [InlineData("125,50")]
    [InlineData("1250000,50")]
    [InlineData("1,250,000.50")]
    [InlineData("1e5")]
    [InlineData("1E5")]
    [InlineData("1.5e2")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    [InlineData("∞")]
    [InlineData("1 000")]
    [InlineData("$100")]
    [InlineData("100 DKK")]
    [InlineData("(100)")]
    [InlineData("0x10")]
    [InlineData("--5")]
    [InlineData("1.2.3")]
    [InlineData("5-")]
    [InlineData("-0")]
    public void Invalid_numbers_give_InvalidNumber_with_exact_message(string text)
    {
        foreach (var column in AmountColumns)
        {
            var problem = SingleProblem(Validate((column, text)));

            Assert.Equal("InvalidNumber", problem.Code);
            Assert.Equal(column, problem.Field);
            Assert.Equal(
                $"This lease can't be processed because '{column}' has the value '{text.Trim()}', which is not a valid amount. " +
                "Use digits with '.' as the decimal point and no thousands separators, e.g. 1250000.50.",
                problem.Message);
        }
    }

    // Strict number format (CLAUDE.md section 5): ^[+-]?(0|[1-9][0-9]*)(\.[0-9]+)?$ and no minus on zero.
    // A value that fails the format gets ONLY InvalidNumber (no TooManyDecimals / NegativeAmount).
    public static readonly TheoryData<string> StrictFormatViolations = new()
    {
        "1.", ".5", "00012", "01.5", "-0", "-0.00", "-00012", "-.5", "+.5", "-1.", "00", "-01.234",
    };

    [Theory]
    [MemberData(nameof(StrictFormatViolations))]
    public void Strict_format_violations_give_only_InvalidNumber_with_exact_message(string text)
    {
        foreach (var column in AmountColumns)
        {
            var problem = SingleProblem(Validate((column, text)));

            Assert.Equal(new Problem(Line, "L-1", column, "InvalidNumber",
                $"This lease can't be processed because '{column}' has the value '{text}', which is not a valid amount. " +
                "Use digits with '.' as the decimal point and no thousands separators, e.g. 1250000.50."),
                problem);
        }
    }

    public static readonly TheoryData<string, string> StrictFormatValidValues = new()
    {
        { "0", "0" },
        { "0.50", "0.50" },
        { "+0", "0" },
        { "+500.00", "500.00" },
        { "0.5", "0.5" },
        { "10", "10" },
    };

    [Theory]
    [MemberData(nameof(StrictFormatValidValues))]
    public void Strict_format_valid_values_are_accepted(string text, string expected)
    {
        var result = Validate(("Opening", text), ("Additions", text), ("Depreciation", text), ("Disposals", text));

        Assert.Empty(result.Problems);
        var value = decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(value, result.Lease!.Opening);
        Assert.Equal(value, result.Lease.Additions);
        Assert.Equal(value, result.Lease.Depreciation);
        Assert.Equal(value, result.Lease.Disposals);
    }

    [Fact]
    public void Negative_value_with_too_many_decimals_gives_TooManyDecimals_and_NegativeAmount()
    {
        foreach (var column in AmountColumns)
        {
            var result = Validate((column, "-1.234"));

            Assert.Null(result.Lease);
            Assert.Equal(2, result.Problems.Count);
            Assert.Contains(new Problem(Line, "L-1", column, "TooManyDecimals",
                $"This lease can't be processed because '{column}' has the value '-1.234', which has more than 2 decimals."),
                result.Problems);
            Assert.Contains(new Problem(Line, "L-1", column, "NegativeAmount",
                $"This lease can't be processed because '{column}' has the value '-1.234', which is negative. Amounts must be 0 or more."),
                result.Problems);
        }
    }

    [Fact]
    public void InvalidNumber_message_uses_the_trimmed_value()
    {
        var problem = SingleProblem(Validate(("Opening", "  abc  ")));

        Assert.Contains("has the value 'abc',", problem.Message);
    }

    // ---------- TooManyDecimals ----------

    [Theory]
    [InlineData("1.250")]
    [InlineData("0.001")]
    [InlineData("0.000")]
    [InlineData("100.123")]
    [InlineData("1.00000")]
    public void More_than_two_decimals_gives_TooManyDecimals_with_exact_message(string text)
    {
        foreach (var column in AmountColumns)
        {
            var problem = SingleProblem(Validate((column, text)));

            Assert.Equal("TooManyDecimals", problem.Code);
            Assert.Equal(column, problem.Field);
            Assert.Equal(
                $"This lease can't be processed because '{column}' has the value '{text}', which has more than 2 decimals.",
                problem.Message);
        }
    }

    // ---------- NegativeAmount ----------

    [Theory]
    [InlineData("-1")]
    [InlineData("-0.01")]
    [InlineData("-500.00")]
    [InlineData("-1250000.50")]
    public void Negative_amount_gives_NegativeAmount_with_exact_message(string text)
    {
        foreach (var column in AmountColumns)
        {
            var problem = SingleProblem(Validate((column, text)));

            Assert.Equal("NegativeAmount", problem.Code);
            Assert.Equal(column, problem.Field);
            Assert.Equal(
                $"This lease can't be processed because '{column}' has the value '{text}', which is negative. Amounts must be 0 or more.",
                problem.Message);
        }
    }

    [Fact]
    public void Validator_does_not_check_closing_value()
    {
        // Closing = 0 + 0 - 1000 - 0 = -1000: C# must accept it (closing belongs to JScript).
        var result = Validate(("Opening", "0"), ("Additions", "0"), ("Depreciation", "1000.00"), ("Disposals", "0"));

        Assert.Empty(result.Problems);
        Assert.NotNull(result.Lease);
    }

    // ---------- InvalidCurrency ----------

    [Theory]
    [InlineData("dkk")]
    [InlineData("Dkk")]
    [InlineData("DK")]
    [InlineData("DKKK")]
    [InlineData("D K K")]
    [InlineData("XYZ")]
    [InlineData("123")]
    [InlineData("kr.")]
    [InlineData("€")]
    // retired codes
    [InlineData("BGN")]
    [InlineData("HRK")]
    [InlineData("ANG")]
    [InlineData("SLL")]
    [InlineData("ZWL")]
    [InlineData("CUC")]
    // fund, precious-metal, test and no-currency codes
    [InlineData("XXX")]
    [InlineData("XTS")]
    [InlineData("XAU")]
    [InlineData("XAG")]
    [InlineData("XPT")]
    [InlineData("XPD")]
    [InlineData("XDR")]
    [InlineData("USN")]
    [InlineData("CLF")]
    public void Invalid_or_inactive_currency_gives_InvalidCurrency_with_exact_message(string currency)
    {
        var problem = SingleProblem(Validate(("Currency", currency)));

        Assert.Equal("InvalidCurrency", problem.Code);
        Assert.Equal("Currency", problem.Field);
        Assert.Equal(Line, problem.Line);
        Assert.Equal(
            $"This lease can't be processed because '{currency}' is not a valid ISO-4217 currency code (use three uppercase letters, e.g. DKK).",
            problem.Message);
    }

    [Fact]
    public void Currency_is_trimmed_before_checking()
    {
        var result = Validate(("Currency", "  EUR "));

        Assert.Empty(result.Problems);
        Assert.Equal("EUR", result.Lease!.Currency);
    }

    // ---------- several problems ----------

    [Fact]
    public void Every_problem_in_a_row_is_reported()
    {
        var result = Validate(
            ("LeaseId", ""), ("AssetClass", " "), ("Currency", "dkk"),
            ("Opening", "abc"), ("Additions", "1.234"), ("Depreciation", "-5"), ("Disposals", ""));

        Assert.Null(result.Lease);
        var actual = result.Problems.Select(p => (p.Field, p.Code)).OrderBy(x => x.Field).ToArray();
        var expected = new (string?, string)[]
        {
            ("Additions", "TooManyDecimals"),
            ("AssetClass", "MissingValue"),
            ("Currency", "InvalidCurrency"),
            ("Depreciation", "NegativeAmount"),
            ("Disposals", "MissingValue"),
            ("LeaseId", "MissingValue"),
            ("Opening", "InvalidNumber"),
        }.OrderBy(x => x.Item1).ToArray();
        Assert.Equal(expected, actual);
        Assert.All(result.Problems, p => Assert.Equal(Line, p.Line));
    }

    [Fact]
    public void Problems_in_all_four_amount_columns_are_reported_together()
    {
        var result = Validate(("Opening", "x"), ("Additions", "y"), ("Depreciation", "1,5"), ("Disposals", "NaN"));

        Assert.Equal(4, result.Problems.Count);
        Assert.All(result.Problems, p => Assert.Equal("InvalidNumber", p.Code));
        Assert.Equal(AmountColumns.OrderBy(c => c), result.Problems.Select(p => p.Field!).OrderBy(c => c));
    }

    // ---------- ProblemMessages agrees with the CLAUDE.md section 4 table ----------

    [Fact]
    public void ProblemMessages_match_the_agreed_text()
    {
        Assert.Equal("The file can't be processed because it is empty (no header line was found).",
            ProblemMessages.EmptyFile());
        Assert.Equal("The file can't be processed because the required column 'Opening' is missing from the header.",
            ProblemMessages.MissingColumn("Opening"));
        Assert.Equal("The file can't be processed because the column 'Opening' appears more than once in the header.",
            ProblemMessages.DuplicateColumn("Opening"));
        Assert.Equal("This lease can't be processed because a quoted value starting on line 12 is never closed.",
            ProblemMessages.UnclosedQuote(12));
        Assert.Equal("This lease can't be processed because it has 7 values, but the header has 8 columns.",
            ProblemMessages.InvalidFieldCount(7, 8));
        Assert.Equal("This lease can't be processed because 'Currency' is empty.",
            ProblemMessages.MissingValue("Currency"));
        Assert.Equal("This lease can't be processed because 'Opening' has the value '1e5', which is not a valid amount. Use digits with '.' as the decimal point and no thousands separators, e.g. 1250000.50.",
            ProblemMessages.InvalidNumber("Opening", "1e5"));
        Assert.Equal("This lease can't be processed because 'Opening' has the value '1.250', which has more than 2 decimals.",
            ProblemMessages.TooManyDecimals("Opening", "1.250"));
        Assert.Equal("This lease can't be processed because 'Opening' has the value '-5', which is negative. Amounts must be 0 or more.",
            ProblemMessages.NegativeAmount("Opening", "-5"));
        Assert.Equal("This lease can't be processed because 'dkk' is not a valid ISO-4217 currency code (use three uppercase letters, e.g. DKK).",
            ProblemMessages.InvalidCurrency("dkk"));
        Assert.Equal("This lease can't be processed because its ID 'L-2001' is not unique (it already appears on line 2).",
            ProblemMessages.DuplicateLeaseId("L-2001", 2));
    }

    [Fact]
    public void Problem_codes_are_the_agreed_strings()
    {
        Assert.Equal(
            new[] { "EmptyFile", "MissingColumn", "DuplicateColumn", "UnclosedQuote", "InvalidFieldCount", "MissingValue",
                    "InvalidNumber", "TooManyDecimals", "NegativeAmount", "InvalidCurrency", "DuplicateLeaseId" },
            new[] { General.ProblemMessages.EmptyFile, General.ProblemMessages.MissingColumn, General.ProblemMessages.DuplicateColumn, General.ProblemMessages.UnclosedQuote,
                    General.ProblemMessages.InvalidFieldCount, General.ProblemMessages.MissingValue, General.ProblemMessages.InvalidNumber, General.ProblemMessages.TooManyDecimals,
                    General.ProblemMessages.NegativeAmount, General.ProblemMessages.InvalidCurrency, General.ProblemMessages.DuplicateLeaseId });
    }
}
