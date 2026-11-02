using System.Text.Json;
using LeaseImport.Common;
using static LeaseImport.Tests.TestData;

namespace LeaseImport.Tests;

public class LeaseImporterTests
{
    private const string Row1 = "L-1,Property,Office,DKK,100.00,0,10.00,0";
    private const string Row2 = "L-2,Vehicles,Car,DKK,200.00,0,20.00,0";

    private static readonly string[] RequiredColumns =
        ["LeaseId", "AssetClass", "Description", "Currency", "Opening", "Additions", "Depreciation", "Disposals"];

    // ---------- sample ----------

    [Fact]
    public void Sample_file_gives_four_accepted_leases_and_no_problems()
    {
        var result = ImportSample();

        Assert.Empty(result.Problems);
        Assert.Equal(
            new[]
            {
                new Lease(2, "L-2001", "Property", "Office Aarhus", "DKK", 1250000.50m, 0m, 125000.05m, 0m),
                new Lease(3, "L-2002", "Vehicles", "Fleet (12 cars)", "DKK", 480000.00m, 60000.00m, 96000.00m, 0m),
                new Lease(4, "L-2003", "Equipment", "Printer", "DKK", 20000.00m, 0m, 5000.00m, 0m),
                new Lease(5, "L-2004", "Property", "Warehouse Odense", "EUR", 300000.00m, 0m, 30000.00m, 0m),
            },
            result.Leases);
    }

    [Fact]
    public void Sample_json_contains_contract_values()
    {
        using var doc = JsonDocument.Parse(ToJson(ImportSample()));
        var first = doc.RootElement.GetProperty("Leases")[0];

        Assert.Equal(4, doc.RootElement.GetProperty("Leases").GetArrayLength());
        Assert.Equal(0, doc.RootElement.GetProperty("Problems").GetArrayLength());
        Assert.Equal(2, first.GetProperty("Line").GetInt32());
        Assert.Equal("L-2001", first.GetProperty("LeaseId").GetString());
        Assert.Equal("1250000.50", first.GetProperty("Opening").GetRawText());
        Assert.Equal("0", first.GetProperty("Additions").GetRawText());
        Assert.Equal("125000.05", first.GetProperty("Depreciation").GetRawText());
        Assert.Equal(JsonValueKind.Number, first.GetProperty("Disposals").ValueKind);
    }

    // ---------- header ----------

    [Fact]
    public void Header_columns_may_be_in_any_order()
    {
        var result = Import(Csv(
            "Disposals,Depreciation,Additions,Opening,Currency,Description,AssetClass,LeaseId",
            "0,10.00,5.00,100.00,EUR,Office,Property,L-1"));

        Assert.Empty(result.Problems);
        Assert.Equal(new Lease(2, "L-1", "Property", "Office", "EUR", 100.00m, 5.00m, 10.00m, 0m), Assert.Single(result.Leases));
    }

    [Fact]
    public void Header_names_are_trimmed()
    {
        var result = Import(Csv(
            " LeaseId , AssetClass,Description ,Currency,Opening,Additions,Depreciation,Disposals ",
            Row1));

        Assert.Empty(result.Problems);
        Assert.Single(result.Leases);
    }

    [Fact]
    public void Extra_columns_are_ignored_even_when_duplicated()
    {
        var result = Import(Csv(
            "Notes,LeaseId,AssetClass,Description,Currency,Opening,Additions,Depreciation,Disposals,Notes,Owner",
            "whatever,L-1,Property,Office,DKK,100.00,0,10.00,0,more,Bob"));

        Assert.Empty(result.Problems);
        Assert.Equal(new Lease(2, "L-1", "Property", "Office", "DKK", 100.00m, 0m, 10.00m, 0m), Assert.Single(result.Leases));
    }

    [Theory]
    [InlineData("LeaseId")]
    [InlineData("AssetClass")]
    [InlineData("Description")]
    [InlineData("Currency")]
    [InlineData("Opening")]
    [InlineData("Additions")]
    [InlineData("Depreciation")]
    [InlineData("Disposals")]
    public void Missing_required_column_is_fatal(string missing)
    {
        var columns = RequiredColumns.Where(c => c != missing).ToArray();
        var row = string.Join(",", columns.Select(_ => "1"));

        var result = Import(Csv(string.Join(",", columns), row, row));

        Assert.Empty(result.Leases);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(new Problem(1, null, missing, "MissingColumn",
            $"The file can't be processed because the required column '{missing}' is missing from the header."), problem);
    }

    [Fact]
    public void Header_match_is_case_sensitive()
    {
        var result = Import(Csv(
            "leaseid,AssetClass,Description,Currency,Opening,Additions,Depreciation,Disposals",
            Row1));

        Assert.Empty(result.Leases);
        var problem = Assert.Single(result.Problems);
        Assert.Equal("MissingColumn", problem.Code);
        Assert.Equal("LeaseId", problem.Field);
    }

    [Fact]
    public void Duplicate_required_column_is_fatal_with_exact_message()
    {
        var result = Import(Csv(Header + ",Opening", Row1 + ",5", Row2 + ",6"));

        Assert.Empty(result.Leases);
        var problem = Assert.Single(result.Problems);
        Assert.Equal(new Problem(1, null, "Opening", "DuplicateColumn",
            "The file can't be processed because the column 'Opening' appears more than once in the header."), problem);
    }

    [Fact]
    public void Several_header_problems_are_all_reported_and_rows_are_not_validated()
    {
        var result = Import(Csv(
            "LeaseId,LeaseId,AssetClass,Description,Opening,Additions,Depreciation",
            "L-1,L-1,Property,Office,abc,0,0"));

        Assert.Empty(result.Leases);
        Assert.All(result.Problems, p => Assert.Equal(1, p.Line));
        Assert.All(result.Problems, p => Assert.Null(p.LeaseId));
        Assert.Equal(
            new[] { ("Currency", "MissingColumn"), ("Disposals", "MissingColumn"), ("LeaseId", "DuplicateColumn") },
            result.Problems.Select(p => (p.Field!, p.Code)).OrderBy(x => x.Item1).ToArray());
    }

    // ---------- empty / header only ----------

    [Theory]
    [InlineData("")]
    [InlineData("\uFEFF")]
    [InlineData("\n\n")]
    [InlineData("\r\n  \r\n")]
    public void Empty_file_gives_EmptyFile(string csv)
    {
        var result = Import(csv);

        Assert.Empty(result.Leases);
        Assert.Equal(new Problem(1, null, null, "EmptyFile",
            "The file can't be processed because it is empty (no header line was found)."), Assert.Single(result.Problems));
    }

    [Theory]
    [InlineData(Header)]
    [InlineData(Header + "\r\n")]
    [InlineData("\uFEFF" + Header + "\n\n\n")]
    public void Header_only_is_valid_with_no_leases_and_no_problems(string csv)
    {
        var result = Import(csv);

        Assert.Empty(result.Leases);
        Assert.Empty(result.Problems);
    }

    // ---------- rows ----------

    [Fact]
    public void Line_numbers_count_blank_lines_and_multiline_quoted_fields()
    {
        var result = Import(Csv(
            Header,
            "",
            "L-1,Property,\"Office",
            "second line\",DKK,100.00,0,10.00,0",
            "   ",
            Row2));

        Assert.Empty(result.Problems);
        Assert.Equal([3, 6], result.Leases.Select(l => l.Line));
        Assert.Equal("Office\nsecond line", result.Leases[0].Description);
    }

    [Fact]
    public void Quoted_values_with_commas_and_escaped_quotes_are_imported()
    {
        var result = Import(Csv(Header, "L-1,Property,\"Office, \"\"Aarhus\"\"\",DKK,\"100.00\",0,10.00,0"));

        Assert.Empty(result.Problems);
        Assert.Equal("Office, \"Aarhus\"", Assert.Single(result.Leases).Description);
    }

    [Theory]
    [InlineData("L-1,Property,Office,DKK,100.00,0,10.00", 7)]
    [InlineData("L-1,Property,Office,DKK,100.00,0,10.00,0,extra", 9)]
    [InlineData("L-1", 1)]
    [InlineData("L-1,Property,Office, Aarhus,DKK,100.00,0,10.00,0", 9)]
    public void Wrong_field_count_rejects_row_with_exact_message(string row, int actual)
    {
        var result = Import(Csv(Header, Row1, row, Row2));

        Assert.Equal(["L-1", "L-2"], result.Leases.Select(l => l.LeaseId));
        Assert.Equal(new Problem(3, null, null, "InvalidFieldCount",
            $"This lease can't be processed because it has {actual} values, but the header has 8 columns."),
            Assert.Single(result.Problems));
    }

    [Fact]
    public void Field_count_is_compared_with_the_header_including_extra_columns()
    {
        var result = Import(Csv(Header + ",Notes", Row1));

        Assert.Empty(result.Leases);
        var problem = Assert.Single(result.Problems);
        Assert.Equal("InvalidFieldCount", problem.Code);
        Assert.Equal("This lease can't be processed because it has 8 values, but the header has 9 columns.", problem.Message);
    }

    [Fact]
    public void Rejected_rows_are_not_in_Leases_and_valid_rows_are()
    {
        var result = Import(Csv(Header, Row1, "L-X,Property,Office,dkk,100.00,0,10.00,0", Row2));

        Assert.Equal(["L-1", "L-2"], result.Leases.Select(l => l.LeaseId));
        Assert.Equal(new Problem(3, "L-X", "Currency", "InvalidCurrency",
            "This lease can't be processed because 'dkk' is not a valid ISO-4217 currency code (use three uppercase letters, e.g. DKK)."),
            Assert.Single(result.Problems));
    }

    [Fact]
    public void Negative_closing_is_not_a_problem_in_csharp()
    {
        var result = Import(Csv(Header, "L-1,Property,Office,DKK,0,0,500.00,100.00"));

        Assert.Empty(result.Problems);
        Assert.Single(result.Leases);
    }

    [Fact]
    public void Unclosed_quote_in_data_row_is_reported_and_earlier_rows_are_kept()
    {
        var result = Import(Csv(Header, Row1, "", "L-2,Property,\"Office,DKK,100.00,0,10.00,0", Row2));

        Assert.Equal(["L-1"], result.Leases.Select(l => l.LeaseId));
        Assert.Equal(new Problem(4, null, null, "UnclosedQuote",
            "This lease can't be processed because a quoted value starting on line 4 is never closed."),
            Assert.Single(result.Problems));
    }

    [Fact]
    public void Unclosed_quote_in_header_gives_only_UnclosedQuote()
    {
        var result = Import(Csv("LeaseId,\"AssetClass,Description,Currency,Opening,Additions,Depreciation,Disposals", Row1));

        Assert.Empty(result.Leases);
        Assert.Equal(new Problem(1, null, null, "UnclosedQuote",
            "This lease can't be processed because a quoted value starting on line 1 is never closed."),
            Assert.Single(result.Problems));
    }

    // ---------- duplicates ----------

    [Fact]
    public void Duplicate_LeaseId_keeps_the_first_and_rejects_later_ones()
    {
        var result = Import(Csv(
            Header,
            "L-2001,Property,Office Aarhus,DKK,1250000.50,0,125000.05,0",
            Row1,
            "",
            "L-2001,Property,Copy,DKK,1.00,0,0,0",
            "L-2001,Property,Copy 2,DKK,2.00,0,0,0"));

        Assert.Equal(["L-2001", "L-1"], result.Leases.Select(l => l.LeaseId));
        Assert.Equal("Office Aarhus", result.Leases[0].Description);
        Assert.Equal(
            new[]
            {
                new Problem(5, "L-2001", "LeaseId", "DuplicateLeaseId",
                    "This lease can't be processed because its ID 'L-2001' is not unique (it already appears on line 2)."),
                new Problem(6, "L-2001", "LeaseId", "DuplicateLeaseId",
                    "This lease can't be processed because its ID 'L-2001' is not unique (it already appears on line 2)."),
            },
            result.Problems);
    }

    [Fact]
    public void Duplicate_comparison_is_after_trimming_and_case_sensitive()
    {
        var result = Import(Csv(
            Header,
            "L-1,Property,A,DKK,1,0,0,0",
            "  L-1  ,Property,B,DKK,1,0,0,0",
            "l-1,Property,C,DKK,1,0,0,0"));

        Assert.Equal(["L-1", "l-1"], result.Leases.Select(l => l.LeaseId));
        var problem = Assert.Single(result.Problems);
        Assert.Equal("DuplicateLeaseId", problem.Code);
        Assert.Equal(3, problem.Line);
        Assert.Equal("L-1", problem.LeaseId);
    }

    [Fact]
    public void Id_of_a_rejected_row_still_counts_as_seen()
    {
        var result = Import(Csv(
            Header,
            "L-1,Property,A,XYZ,1,0,0,0",
            "L-1,Property,B,DKK,1,0,0,0"));

        Assert.Empty(result.Leases);
        Assert.Equal(2, result.Problems.Count);
        Assert.Equal("InvalidCurrency", result.Problems[0].Code);
        Assert.Equal(new Problem(3, "L-1", "LeaseId", "DuplicateLeaseId",
            "This lease can't be processed because its ID 'L-1' is not unique (it already appears on line 2)."),
            result.Problems[1]);
    }

    [Fact]
    public void Duplicate_problem_is_listed_first_and_other_problems_of_the_row_follow()
    {
        var result = Import(Csv(
            Header,
            Row1,
            "L-1,,A,DKK,abc,0,0,0"));

        Assert.Single(result.Leases);
        Assert.Equal(3, result.Problems.Count);
        Assert.Equal("DuplicateLeaseId", result.Problems[0].Code);
        Assert.Equal(
            new[] { ("AssetClass", "MissingValue"), ("Opening", "InvalidNumber") },
            result.Problems.Skip(1).Select(p => (p.Field!, p.Code)).OrderBy(x => x.Item1).ToArray());
        Assert.All(result.Problems, p => Assert.Equal(3, p.Line));
        Assert.All(result.Problems, p => Assert.Equal("L-1", p.LeaseId));
    }

    [Fact]
    public void Row_with_wrong_field_count_does_not_mark_its_id_as_seen()
    {
        var result = Import(Csv(
            Header,
            "L-1,Property,A,DKK,1,0,0",
            "L-1,Property,B,DKK,1,0,0,0"));

        Assert.Equal(new Lease(3, "L-1", "Property", "B", "DKK", 1m, 0m, 0m, 0m), Assert.Single(result.Leases));
        Assert.Equal("InvalidFieldCount", Assert.Single(result.Problems).Code);
    }

    [Fact]
    public void Missing_LeaseIds_are_not_duplicates_of_each_other()
    {
        var result = Import(Csv(Header, ",Property,A,DKK,1,0,0,0", " ,Property,B,DKK,1,0,0,0"));

        Assert.Empty(result.Leases);
        Assert.Equal(2, result.Problems.Count);
        Assert.All(result.Problems, p => Assert.Equal("MissingValue", p.Code));
        Assert.All(result.Problems, p => Assert.Null(p.LeaseId));
    }

    // ---------- JSON shape ----------

    [Fact]
    public void Json_has_exact_contract_shape_with_nulls_written_and_no_severity()
    {
        var result = Import(Csv(Header, Row1, "L-2,Property,,DKK,1,0,0", ",Property,B,DKK,1,0,0,0"));

        var json = ToJson(result);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(["Leases", "Problems"], root.EnumerateObject().Select(p => p.Name));
        var lease = Assert.Single(root.GetProperty("Leases").EnumerateArray());
        Assert.Equal(
            ["Line", "LeaseId", "AssetClass", "Description", "Currency", "Opening", "Additions", "Depreciation", "Disposals"],
            lease.EnumerateObject().Select(p => p.Name));
        Assert.Equal(JsonValueKind.Number, lease.GetProperty("Line").ValueKind);
        Assert.Equal("100.00", lease.GetProperty("Opening").GetRawText());

        var problems = root.GetProperty("Problems").EnumerateArray().ToArray();
        Assert.Equal(2, problems.Length);
        foreach (var problem in problems)
        {
            Assert.Equal(["Line", "LeaseId", "Field", "Code", "Message"], problem.EnumerateObject().Select(p => p.Name));
        }

        var fieldCount = problems[0];
        Assert.Equal(3, fieldCount.GetProperty("Line").GetInt32());
        Assert.Equal(JsonValueKind.Null, fieldCount.GetProperty("LeaseId").ValueKind);
        Assert.Equal(JsonValueKind.Null, fieldCount.GetProperty("Field").ValueKind);
        Assert.Equal("InvalidFieldCount", fieldCount.GetProperty("Code").GetString());

        var missingId = problems[1];
        Assert.Equal(JsonValueKind.Null, missingId.GetProperty("LeaseId").ValueKind);
        Assert.Equal("LeaseId", missingId.GetProperty("Field").GetString());

        Assert.DoesNotContain("Severity", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("leaseId", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_of_empty_file_writes_null_field()
    {
        using var doc = JsonDocument.Parse(ToJson(Import("")));
        var problem = doc.RootElement.GetProperty("Problems")[0];

        Assert.Equal(0, doc.RootElement.GetProperty("Leases").GetArrayLength());
        Assert.Equal(1, problem.GetProperty("Line").GetInt32());
        Assert.Equal(JsonValueKind.Null, problem.GetProperty("Field").ValueKind);
        Assert.Equal(JsonValueKind.Null, problem.GetProperty("LeaseId").ValueKind);
        Assert.Equal("EmptyFile", problem.GetProperty("Code").GetString());
    }

    [Fact]
    public void Json_round_trips_non_ascii_text_and_quotes()
    {
        var result = Import(Csv(Header, "L-1,Property,\"Kontor Århus \"\"Øst\"\" & <Co>\",DKK,1,0,0,0"));

        using var doc = JsonDocument.Parse(ToJson(result));

        Assert.Equal("Kontor Århus \"Øst\" & <Co>", doc.RootElement.GetProperty("Leases")[0].GetProperty("Description").GetString());
    }

    // ---------- NBSP-only line (CLAUDE.md section 5) ----------

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Line_with_only_a_non_breaking_space_is_not_blank_and_gives_InvalidFieldCount(string newline)
    {
        var csv = string.Join(newline, Header, Row1, "", "\u00A0", "  \t ", Row2) + newline;

        var result = Import(csv);

        Assert.Equal(new[] { 2, 6 }, result.Leases.Select(l => l.Line).ToArray());
        Assert.Equal(new Problem(4, null, null, "InvalidFieldCount",
            "This lease can't be processed because it has 1 values, but the header has 8 columns."),
            Assert.Single(result.Problems));
    }

    [Fact]
    public void Line_with_non_breaking_spaces_around_spaces_is_also_InvalidFieldCount()
    {
        var result = Import(Csv(Header, " \u00A0 ", Row1));

        Assert.Equal(new Problem(2, null, null, "InvalidFieldCount",
            "This lease can't be processed because it has 1 values, but the header has 8 columns."),
            Assert.Single(result.Problems));
        Assert.Equal(3, Assert.Single(result.Leases).Line);
    }

    // ---------- WriteJson (streaming) ----------

    [Fact]
    public void WriteJson_writes_utf8_without_bom_and_keeps_danish_letters()
    {
        var result = Import(Csv(Header, Row1, "L-2,Vehicles,Bil \"København\",dkk,1.250,0,0,0", "x,y", "L-3,Property,Kontor København,DKK,1,0,0,0"));

        using var stream = new MemoryStream();
        LeaseImporter.WriteJson(result, stream);
        var bytes = stream.ToArray();

        Assert.True(bytes.Length > 0);
        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF, "WriteJson must not write a BOM");
        Assert.Equal((byte)'{', bytes[0]);
        var text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
        Assert.Contains("København", text);
        using var doc = JsonDocument.Parse(text);
        Assert.Equal(2, doc.RootElement.GetProperty("Leases").GetArrayLength());
    }
}
