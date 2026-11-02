using System.Text.Json;
using LeaseImport.Common;
using static LeaseImport.Common.General.LeaseImporter;
using static LeaseImport.Common.General.LeaseValidator;

namespace LeaseImport;

/// <summary>
/// Orchestrates one import: header check, field counts, value validation, duplicate LeaseIds.
/// The input is read record by record; only the result (leases and problems) is kept in memory.
/// </summary>
internal static class LeaseImporter
{
    public static ImportResult Import(TextReader reader)
    {
        var leases = new List<Lease>();
        var problems = new List<Problem>();
        var parserProblems = new List<Problem>();

        using var records = CsvParser.ReadRecords(reader, parserProblems).GetEnumerator();

        if (!records.MoveNext())
        {
            if (parserProblems.Count == 0)
            {
                problems.Add(new Problem(HeaderLine, null, null, General.ProblemMessages.EmptyFile, ProblemMessages.EmptyFile()));
            }
            problems.AddRange(parserProblems);
            return new ImportResult(leases, problems);
        }

        var header = records.Current.Fields;
        var headerProblems = CheckHeader(header);
        if (headerProblems.Count > 0)
        {
            ReadToEnd(records);
            return new ImportResult(leases, headerProblems);
        }

        var columnIndex = BuildColumnIndex(header);
        var firstLineById = new Dictionary<string, int>(StringComparer.Ordinal);

        while (records.MoveNext())
        {
            var record = records.Current;
            if (record.Fields.Count != header.Count)
            {
                problems.Add(new Problem(record.StartLine, null, null, General.ProblemMessages.InvalidFieldCount,
                    ProblemMessages.InvalidFieldCount(record.Fields.Count, header.Count)));
                continue;
            }

            var result = LeaseValidator.Validate(record.StartLine, record.Fields, columnIndex);

            Problem? duplicate = null;
            if (result.LeaseId is { } id)
            {
                if (firstLineById.TryGetValue(id, out var firstLine))
                {
                    duplicate = new Problem(record.StartLine, id, LeaseIdColumn,
                        General.ProblemMessages.DuplicateLeaseId, ProblemMessages.DuplicateLeaseId(id, firstLine));
                }
                else
                {
                    firstLineById.Add(id, record.StartLine);
                }
            }

            if (duplicate is not null)
            {
                problems.Add(duplicate);
            }
            problems.AddRange(result.Problems);

            if (duplicate is null && result.Lease is not null)
            {
                leases.Add(result.Lease);
            }
        }

        problems.AddRange(parserProblems);
        return new ImportResult(leases, problems);
    }

    /// <summary>Writes the result as UTF-8 JSON straight to <paramref name="output"/>.</summary>
    public static void WriteJson(ImportResult result, Stream output) =>
        JsonSerializer.Serialize(output, result, JsonOptions);

    public static string ToJson(ImportResult result) => JsonSerializer.Serialize(result, JsonOptions);

    /// <summary>
    /// Reads the rest of the input after a fatal header, so that the whole file is still decoded
    /// (invalid UTF-8 anywhere makes the input unreadable). Its records are not used.
    /// </summary>
    private static void ReadToEnd(IEnumerator<CsvRecord> records)
    {
        while (records.MoveNext())
        {
        }
    }

    private static List<Problem> CheckHeader(IReadOnlyList<string> header)
    {
        var problems = new List<Problem>();
        foreach (var column in RequiredColumns)
        {
            var count = header.Count(name => string.Equals(name.Trim(), column, StringComparison.Ordinal));
            if (count == 0)
            {
                problems.Add(new Problem(HeaderLine, null, column, General.ProblemMessages.MissingColumn,
                    ProblemMessages.MissingColumn(column)));
            }
            else if (count > 1)
            {
                problems.Add(new Problem(HeaderLine, null, column, General.ProblemMessages.DuplicateColumn,
                    ProblemMessages.DuplicateColumn(column)));
            }
        }
        return problems;
    }

    private static Dictionary<string, int> BuildColumnIndex(IReadOnlyList<string> header)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < header.Count; i++)
        {
            var name = header[i].Trim();
            if (RequiredColumns.Contains(name))
            {
                index[name] = i;
            }
        }
        return index;
    }
}
