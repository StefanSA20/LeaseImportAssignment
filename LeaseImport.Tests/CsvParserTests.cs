namespace LeaseImport.Tests;

public class CsvParserTests
{
    private static CsvParseResult Parse(string text) => CsvParser.Parse(new StringReader(text));

    private static string[][] Fields(CsvParseResult result) =>
        result.Records.Select(r => r.Fields.ToArray()).ToArray();

    private static int[] Lines(CsvParseResult result) => result.Records.Select(r => r.StartLine).ToArray();

    [Fact]
    public void Simple_records_get_physical_line_numbers_starting_at_one()
    {
        var result = Parse("a,b,c\n1,2,3\n4,5,6");

        Assert.Empty(result.Problems);
        Assert.Equal([1, 2, 3], Lines(result));
        Assert.Equal(new[] { new[] { "a", "b", "c" }, ["1", "2", "3"], ["4", "5", "6"] }, Fields(result));
    }

    [Theory]
    [InlineData("﻿a,b\r\n1,2\r\n")]
    [InlineData("﻿a,b\n1,2\n")]
    [InlineData("a,b\r\n1,2\r\n")]
    [InlineData("a,b\n1,2\n")]
    [InlineData("a,b\n1,2")]
    [InlineData("a,b\r\n1,2")]
    public void Bom_line_endings_and_trailing_newline_give_the_same_records(string text)
    {
        var result = Parse(text);

        Assert.Empty(result.Problems);
        Assert.Equal([1, 2], Lines(result));
        Assert.Equal(new[] { new[] { "a", "b" }, ["1", "2"] }, Fields(result));
    }

    [Fact]
    public void Bom_is_not_part_of_the_first_header_name()
    {
        var result = Parse("﻿LeaseId,X\n1,2");

        Assert.Equal("LeaseId", result.Records[0].Fields[0]);
    }

    [Fact]
    public void Utf8_bytes_with_bom_through_a_stream_reader_are_parsed()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(System.Text.Encoding.UTF8.GetBytes("a,b\r\næø,å\r\n")).ToArray();
        using var reader = new StreamReader(new MemoryStream(bytes), detectEncodingFromByteOrderMarks: true);

        var result = CsvParser.Parse(reader);

        Assert.Equal(new[] { new[] { "a", "b" }, ["æø", "å"] }, Fields(result));
    }

    [Fact]
    public void Trailing_newline_is_not_a_row()
    {
        var result = Parse("a,b\r\n1,2\r\n");

        Assert.Equal(2, result.Records.Count);
    }

    [Theory]
    [InlineData("a,b\n\n1,2\n\n\n3,4\n")]
    [InlineData("a,b\r\n\r\n1,2\r\n\r\n\r\n3,4\r\n")]
    [InlineData("a,b\n   \n1,2\n\t\n \t \n3,4\n")]
    public void Blank_and_whitespace_only_lines_are_skipped_but_counted(string text)
    {
        var result = Parse(text);

        Assert.Empty(result.Problems);
        Assert.Equal([1, 3, 6], Lines(result));
        Assert.Equal(new[] { new[] { "a", "b" }, ["1", "2"], ["3", "4"] }, Fields(result));
    }

    [Fact]
    public void Blank_lines_before_the_header_are_counted()
    {
        var result = Parse("\n\na,b\n1,2");

        Assert.Equal([3, 4], Lines(result));
    }

    [Fact]
    public void Quoted_field_may_contain_commas()
    {
        var result = Parse("a,b,c\n\"x, y\",\"1,5\",z");

        Assert.Equal(["x, y", "1,5", "z"], result.Records[1].Fields);
    }

    [Fact]
    public void Doubled_quotes_inside_quoted_field_are_an_escaped_quote()
    {
        var result = Parse("a,b\n\"He said \"\"hi\"\"\",\"\"\"\"");

        Assert.Equal(["He said \"hi\"", "\""], result.Records[1].Fields);
    }

    [Fact]
    public void Empty_quoted_field_is_empty_string()
    {
        var result = Parse("a,b,c\n\"\",,\"\"");

        Assert.Equal(["", "", ""], result.Records[1].Fields);
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void Line_break_inside_quotes_belongs_to_the_field_and_record_keeps_its_start_line(string newline)
    {
        var text = "a,b" + newline + "\"first" + newline + "second" + newline + "third\",x" + newline + "1,2" + newline;

        var result = Parse(text);

        Assert.Empty(result.Problems);
        Assert.Equal([1, 2, 5], Lines(result));
        Assert.Equal("first" + newline + "second" + newline + "third", result.Records[1].Fields[0]);
        Assert.Equal("x", result.Records[1].Fields[1]);
    }

    [Fact]
    public void Commas_are_not_split_naively()
    {
        var result = Parse("a,b\n\"1,250,000.50\",2");

        Assert.Equal(2, result.Records[1].Fields.Count);
    }

    [Fact]
    public void Empty_fields_are_kept()
    {
        var result = Parse("a,b,c\n,,");

        Assert.Equal(["", "", ""], result.Records[1].Fields);
    }

    [Fact]
    public void Unclosed_quote_is_reported_at_the_record_start_line()
    {
        var result = Parse("a,b\n1,2\n\"never closed,x\nmore\nlines");

        var problem = Assert.Single(result.Problems);
        Assert.Equal(3, problem.Line);
        Assert.Equal("UnclosedQuote", problem.Code);
        Assert.Null(problem.Field);
        Assert.Null(problem.LeaseId);
        Assert.Equal("This lease can't be processed because a quoted value starting on line 3 is never closed.", problem.Message);
        Assert.Equal([1, 2], Lines(result));
    }

    [Fact]
    public void Unclosed_quote_after_blank_lines_uses_the_physical_line()
    {
        var result = Parse("a,b\n\n\nx,\"open");

        var problem = Assert.Single(result.Problems);
        Assert.Equal(4, problem.Line);
    }

    [Fact]
    public void Stray_quote_inside_unquoted_field_is_kept_literally()
    {
        var result = Parse("a,b\nab\"c,d");

        Assert.Empty(result.Problems);
        Assert.Equal(["ab\"c", "d"], result.Records[1].Fields);
    }

    [Fact]
    public void Empty_input_gives_no_records_and_no_problems()
    {
        var result = Parse("");

        Assert.Empty(result.Records);
        Assert.Empty(result.Problems);
    }

    [Fact]
    public void Untrimmed_values_are_returned_as_is()
    {
        var result = Parse("a,b\n  x  , y ");

        Assert.Equal(["  x  ", " y "], result.Records[1].Fields);
    }
}
