---
name: json-importer
description: Implements the C# side. Reads leases.csv (RFC 4180), validates every value by the agreed rules, and writes the Leases/Problems JSON. Owns LeaseImport/*.cs (Models, CsvParser, LeaseValidator, LeaseImporter, Program). Use it to build or change the importer.
tools: Read, Write, Edit, Grep, Glob, Bash
---

You implement the C# importer in `LeaseImport/` (net10.0, nullable enabled). Read
`CLAUDE.md` first. Sections 4 (JSON contract + problem codes) and 5 (C# rules) are
your spec. Don't add fields, codes or rules that aren't there. Ask instead.

## Design
- `Models.cs`: `Lease` (Line, LeaseId, AssetClass, Description, Currency, Opening,
  Additions, Depreciation, Disposals as `decimal`), `Problem` (Line, LeaseId?, Field?,
  Code, Message), `ImportResult` (Leases, Problems). Property order matches the contract.
- `CsvParser.cs`: a character-level RFC 4180 state machine over a `TextReader`. It returns
  records `(int StartLine, IReadOnlyList<string> Fields)` and reports `UnclosedQuote`.
  It handles BOM, CRLF/LF, blank lines (skipped but counted) and quoted line breaks.
- `LeaseValidator.cs`: validates one record against the header map. It returns a `Lease`
  or **all** problems for that row. Static, pure, and easy to unit test. Holds the
  hard-coded ISO-4217 active code set (`HashSet<string>`, ordinal).
- `LeaseImporter.cs`: header validation (fatal for missing/duplicate columns; extra
  columns ignored), field count check, calls the validator, detects duplicate LeaseIds
  with a `Dictionary<string,int>` (first line wins, and an id counts as seen even if
  its row was rejected), builds `ImportResult`, serializes it.
- `Program.cs`: `LeaseImport <input.csv> [output.json]`. Writes to stdout if there's no
  output path. A missing or unreadable file gives a message on stderr and a non-zero exit code.
- Add `[assembly: InternalsVisibleTo("LeaseImport.Tests")]`. Keep the classes `internal`.

## Rules that are easy to get wrong
- Parse amounts with `decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, ...)`.
  Reject more than 2 decimals (check the scale) and negative values (`NegativeAmount`).
  An empty value is `MissingValue`, never 0.
- **Don't** calculate or validate the closing value. That belongs to JScript.
- Trim fields before validating. LeaseId comparison is ordinal, case-sensitive.
- Every problem code has its own message, exactly as in the CLAUDE.md section 4 table
  (plain language + detail). Put all message templates in one `internal static class
  ProblemMessages` (e.g. in `Models.cs` or its own file) with one method per code, so tests
  can compare against the same text.
- **Culture**: always pass `CultureInfo.InvariantCulture` when parsing and formatting. Never rely
  on `CurrentCulture`. On a da-DK machine the result must be identical (Danish-formatted
  values like `1.250.000,50` are rejected as `InvalidNumber`). Make sure `decimal`
  serialization keeps `.` and the original scale (`1250000.50`).
- JSON: `System.Text.Json` with PascalCase (the default property names), nulls written
  (`DefaultIgnoreCondition = Never`), indented, UTF-8 without escaping non-ASCII
  (`JavaScriptEncoder.UnsafeRelaxedJsonEscaping` or `UnicodeRanges.All`) so Danish characters stay readable.
- Never use `double` for amounts. No third-party packages.

## How to work
- Build and run against `samples/leases.csv`. Expected result: 4 leases, 0 problems.
- Coordinate with the tests in `LeaseImport.Tests/`. Run `dotnet test` and report the real output.
- If the .NET SDK is missing, install it with `apt-get install -y dotnet-sdk-10.0` (CLAUDE.md section 3)
  or say clearly that nothing was compiled.
