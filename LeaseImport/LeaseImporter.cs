using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LeaseImport;

/// <summary>
/// Orchestrates one import: header check, field counts, value validation, duplicate LeaseIds.
/// </summary>
internal static class LeaseImporter
{
    private const int HeaderLine = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static ImportResult Import(TextReader reader)
    {
        var parsed = CsvParser.Parse(reader);
        var leases = new List<Lease>();
        var problems = new List<Problem>();

        if (parsed.Records.Count == 0)
        {
            if (parsed.Problems.Count == 0)
            {
                problems.Add(new Problem(HeaderLine, null, null, ProblemCodes.EmptyFile, ProblemMessages.EmptyFile()));
            }
            problems.AddRange(parsed.Problems);
            return new ImportResult(leases, problems);
        }

        var header = parsed.Records[0].Fields;
        var headerProblems = CheckHeader(header);
        if (headerProblems.Count > 0)
        {
            return new ImportResult(leases, headerProblems);
        }

        var columnIndex = BuildColumnIndex(header);
        var firstLineById = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var record in parsed.Records.Skip(1))
        {
            if (record.Fields.Count != header.Count)
            {
                problems.Add(new Problem(record.StartLine, null, null, ProblemCodes.InvalidFieldCount,
                    ProblemMessages.InvalidFieldCount(record.Fields.Count, header.Count)));
                continue;
            }

            var values = columnIndex.ToDictionary(c => c.Key, c => record.Fields[c.Value], StringComparer.Ordinal);
            var result = LeaseValidator.Validate(record.StartLine, values);

            Problem? duplicate = null;
            if (result.LeaseId is { } id)
            {
                if (firstLineById.TryGetValue(id, out var firstLine))
                {
                    duplicate = new Problem(record.StartLine, id, LeaseValidator.LeaseIdColumn,
                        ProblemCodes.DuplicateLeaseId, ProblemMessages.DuplicateLeaseId(id, firstLine));
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

        problems.AddRange(parsed.Problems);
        return new ImportResult(leases, problems);
    }

    public static string ToJson(ImportResult result) => JsonSerializer.Serialize(result, JsonOptions);

    private static List<Problem> CheckHeader(IReadOnlyList<string> header)
    {
        var problems = new List<Problem>();
        foreach (var column in LeaseValidator.RequiredColumns)
        {
            var count = header.Count(name => string.Equals(name.Trim(), column, StringComparison.Ordinal));
            if (count == 0)
            {
                problems.Add(new Problem(HeaderLine, null, column, ProblemCodes.MissingColumn,
                    ProblemMessages.MissingColumn(column)));
            }
            else if (count > 1)
            {
                problems.Add(new Problem(HeaderLine, null, column, ProblemCodes.DuplicateColumn,
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
            if (LeaseValidator.RequiredColumns.Contains(name))
            {
                index[name] = i;
            }
        }
        return index;
    }
}
