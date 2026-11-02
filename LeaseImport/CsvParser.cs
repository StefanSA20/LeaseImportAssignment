using System.Text;
using LeaseImport.Common;
using static LeaseImport.Common.General.CsvParser;

namespace LeaseImport;

internal sealed record CsvRecord(int StartLine, IReadOnlyList<string> Fields);

/// <summary>
/// RFC 4180 tokenizer. Line numbers are physical lines starting at 1; a record keeps the line
/// it starts on. Blank lines (empty or only spaces/tabs) are skipped but counted. CRLF, LF and a lone CR all end a line.
/// Characters that RFC 4180 doesn't allow (a quote inside an unquoted field, text after a
/// closing quote) are kept literally rather than rejected.
/// </summary>
internal static class CsvParser
{
    private enum State { FieldStart, Unquoted, Quoted, QuoteInQuoted }

    /// <summary>
    /// Yields records lazily. Problems found by the tokenizer (an unclosed quote) are added to
    /// <paramref name="problems"/> when the end of the input is reached.
    /// </summary>
    public static IEnumerable<CsvRecord> ReadRecords(TextReader reader, ICollection<Problem> problems)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var state = State.FieldStart;
        var line = 1;
        var recordStartLine = 1;
        var recordHasContent = false;

        if (reader.Peek() == Bom)
        {
            reader.Read();
        }

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
            state = State.FieldStart;
        }

        CsvRecord? EndRecord()
        {
            EndField();
            var record = recordHasContent ? new CsvRecord(recordStartLine, fields.ToArray()) : null;
            fields.Clear();
            recordHasContent = false;
            return record;
        }

        int next;
        while ((next = reader.Read()) != -1)
        {
            var c = (char)next;
            var lineBreak = ReadLineBreak(c, reader);
            var isLineBreak = lineBreak is not null;

            if (!recordHasContent && !isLineBreak && !IsBlankCharacter(c))
            {
                recordHasContent = true;
                recordStartLine = line;
            }

            switch (state)
            {
                case State.Quoted:
                    if (c == Quote)
                    {
                        state = State.QuoteInQuoted;
                    }
                    else if (lineBreak is not null)
                    {
                        field.Append(lineBreak);
                    }
                    else
                    {
                        field.Append(c);
                    }
                    break;

                case State.QuoteInQuoted when c == Quote:
                    field.Append(Quote);
                    state = State.Quoted;
                    break;

                case State.FieldStart when c == Quote:
                    state = State.Quoted;
                    break;

                default:
                    if (c == Separator)
                    {
                        EndField();
                    }
                    else if (isLineBreak)
                    {
                        if (EndRecord() is { } record)
                        {
                            yield return record;
                        }
                    }
                    else
                    {
                        field.Append(c);
                        state = State.Unquoted;
                    }
                    break;
            }

            if (isLineBreak)
            {
                line++;
            }
        }

        if (state == State.Quoted)
        {
            problems.Add(new Problem(
                recordStartLine,
                LeaseId: null,
                Field: null,
                General.ProblemMessages.UnclosedQuote,
                ProblemMessages.UnclosedQuote(recordStartLine)));
        }
        else if (EndRecord() is { } last)
        {
            yield return last;
        }
    }

    private static bool IsBlankCharacter(char c) => c == ' ' || c == '\t';

    private static string? ReadLineBreak(char c, TextReader reader)
    {
        if (c == '\n')
        {
            return "\n";
        }
        if (c != '\r')
        {
            return null;
        }
        if (reader.Peek() == '\n')
        {
            reader.Read();
            return "\r\n";
        }
        return "\r";
    }
}
