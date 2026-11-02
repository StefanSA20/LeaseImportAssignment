using System.Globalization;
using System.Text.Json;
using LeaseImport.Common;
using static LeaseImport.Tests.TestData;

namespace LeaseImport.Tests;

/// <summary>
/// Most clients are Danish: on da-DK '.' groups thousands and ',' is the decimal point.
/// Parsing and JSON output must not depend on CurrentCulture.
/// </summary>
[Collection("Culture")]
public class DanishCultureTests
{
    private const string Danish = "da-DK";

    [Fact]
    public void CultureScope_sets_and_restores_both_cultures()
    {
        var before = CultureInfo.CurrentCulture.Name;
        var beforeUi = CultureInfo.CurrentUICulture.Name;

        using (new CultureScope(Danish))
        {
            Assert.Equal(Danish, CultureInfo.CurrentCulture.Name);
            Assert.Equal(Danish, CultureInfo.CurrentUICulture.Name);
            Assert.Equal(",", CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
        }

        Assert.Equal(before, CultureInfo.CurrentCulture.Name);
        Assert.Equal(beforeUi, CultureInfo.CurrentUICulture.Name);
    }

    [Theory]
    [InlineData("1250000.50", "1250000.50")]
    [InlineData("0", "0")]
    [InlineData("125000.05", "125000.05")]
    [InlineData("+500.00", "500.00")]
    public void Invariant_amounts_parse_to_the_right_value_under_da_DK(string text, string expected)
    {
        using var scope = new CultureScope(Danish);

        var result = LeaseValidatorTests.Validate(("Opening", text), ("Depreciation", text));

        Assert.Empty(result.Problems);
        var value = decimal.Parse(expected, CultureInfo.InvariantCulture);
        Assert.Equal(value, result.Lease!.Opening);
        Assert.Equal(value, result.Lease.Depreciation);
    }

    [Fact]
    public void Sample_values_are_not_misread_as_grouped_numbers_under_da_DK()
    {
        using var scope = new CultureScope(Danish);

        var lease = ImportSample().Leases[0];

        Assert.Equal(1250000.50m, lease.Opening);
        Assert.NotEqual(125000050m, lease.Opening);
        Assert.Equal(125000.05m, lease.Depreciation);
        Assert.NotEqual(12500005m, lease.Depreciation);
    }

    [Theory]
    [InlineData("1.250.000,50")]
    [InlineData("125,50")]
    [InlineData("1250000,50")]
    public void Danish_formatted_values_are_rejected_with_InvalidNumber_under_da_DK(string text)
    {
        using var scope = new CultureScope(Danish);

        var result = Import(Csv(Header, $"L-1,Property,Office,DKK,\"{text}\",0,0,0"));

        Assert.Empty(result.Leases);
        Assert.Equal(new Problem(2, "L-1", "Opening", "InvalidNumber",
            $"This lease can't be processed because 'Opening' has the value '{text}', which is not a valid amount. " +
            "Use digits with '.' as the decimal point and no thousands separators, e.g. 1250000.50."),
            Assert.Single(result.Problems));
    }

    [Fact]
    public void Danish_grouped_thousand_is_TooManyDecimals_under_da_DK()
    {
        using var scope = new CultureScope(Danish);

        var result = Import(Csv(Header, "L-1,Property,Office,DKK,1.250,0,0,0"));

        Assert.Empty(result.Leases);
        Assert.Equal(new Problem(2, "L-1", "Opening", "TooManyDecimals",
            "This lease can't be processed because 'Opening' has the value '1.250', which has more than 2 decimals."),
            Assert.Single(result.Problems));
    }

    [Fact]
    public void Sample_import_is_the_same_under_da_DK_and_invariant_culture()
    {
        ImportResult invariant;
        string invariantJson;
        using (new CultureScope(""))
        {
            invariant = ImportSample();
            invariantJson = ToJson(invariant);
        }

        ImportResult danish;
        string danishJson;
        using (new CultureScope(Danish))
        {
            danish = ImportSample();
            danishJson = ToJson(danish);
        }

        Assert.Equal(4, danish.Leases.Count);
        Assert.Empty(danish.Problems);
        Assert.Equal(invariant.Leases, danish.Leases);
        Assert.Equal(invariant.Problems, danish.Problems);
        Assert.Equal(invariantJson, danishJson);
    }

    [Fact]
    public void Json_uses_dot_as_decimal_point_under_da_DK()
    {
        using var scope = new CultureScope(Danish);

        var json = ToJson(ImportSample());

        Assert.Contains("1250000.50", json);
        Assert.Contains("125000.05", json);
        Assert.DoesNotContain("1250000,50", json);
        Assert.DoesNotContain("125000,05", json);
        Assert.DoesNotContain("1.250.000", json);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("1250000.50", doc.RootElement.GetProperty("Leases")[0].GetProperty("Opening").GetRawText());
    }

    [Fact]
    public void Problem_messages_with_numbers_are_culture_independent_under_da_DK()
    {
        using var scope = new CultureScope(Danish);

        var rows = new List<string> { Header };
        for (var i = 0; i < 1200; i++)
        {
            rows.Add($"L-{i},Property,Office,DKK,1,0,0,0");
        }
        rows.Add("L-1199,Property,Copy,DKK,1,0,0,0");

        var problem = Assert.Single(Import(Csv(rows.ToArray())).Problems);

        Assert.Equal(1202, problem.Line);
        Assert.Equal("This lease can't be processed because its ID 'L-1199' is not unique (it already appears on line 1201).", problem.Message);
    }

    // ---------- strict number format under da-DK ----------

    [Theory]
    [MemberData(nameof(LeaseValidatorTests.StrictFormatViolations), MemberType = typeof(LeaseValidatorTests))]
    public void Strict_format_violations_give_only_InvalidNumber_under_da_DK(string text)
    {
        using var scope = new CultureScope(Danish);

        foreach (var column in LeaseValidatorTests.AmountColumns)
        {
            var problem = LeaseValidatorTests.SingleProblem(LeaseValidatorTests.Validate((column, text)));

            Assert.Equal("InvalidNumber", problem.Code);
            Assert.Equal(column, problem.Field);
            Assert.Equal(
                $"This lease can't be processed because '{column}' has the value '{text}', which is not a valid amount. " +
                "Use digits with '.' as the decimal point and no thousands separators, e.g. 1250000.50.",
                problem.Message);
        }
    }

    [Theory]
    [MemberData(nameof(LeaseValidatorTests.StrictFormatValidValues), MemberType = typeof(LeaseValidatorTests))]
    public void Strict_format_valid_values_are_accepted_under_da_DK(string text, string expected)
    {
        using var scope = new CultureScope(Danish);

        var result = LeaseValidatorTests.Validate(("Opening", text), ("Additions", text), ("Depreciation", text), ("Disposals", text));

        Assert.Empty(result.Problems);
        var value = decimal.Parse(expected, CultureInfo.InvariantCulture);
        Assert.Equal(value, result.Lease!.Opening);
        Assert.Equal(value, result.Lease.Additions);
        Assert.Equal(value, result.Lease.Depreciation);
        Assert.Equal(value, result.Lease.Disposals);
    }

    [Fact]
    public void Negative_value_with_too_many_decimals_gives_both_problems_under_da_DK()
    {
        using var scope = new CultureScope(Danish);

        var result = Import(Csv(Header, "L-1,Property,Office,DKK,-1.234,0,0,0"));

        Assert.Empty(result.Leases);
        Assert.Equal(2, result.Problems.Count);
        Assert.Contains(new Problem(2, "L-1", "Opening", "TooManyDecimals",
            "This lease can't be processed because 'Opening' has the value '-1.234', which has more than 2 decimals."),
            result.Problems);
        Assert.Contains(new Problem(2, "L-1", "Opening", "NegativeAmount",
            "This lease can't be processed because 'Opening' has the value '-1.234', which is negative. Amounts must be 0 or more."),
            result.Problems);
    }

    [Fact]
    public void WriteJson_uses_dot_as_decimal_point_under_da_DK()
    {
        using var scope = new CultureScope(Danish);
        var result = ImportSample();

        using var stream = new MemoryStream();
        LeaseImporter.WriteJson(result, stream);
        var json = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.Contains("1250000.50", json);
        Assert.DoesNotContain("1250000,50", json);
    }
}
