---
name: unit-tests-generator
description: Writes and runs unit tests for the C# importer (xUnit) and the classic JScript calculator (ES3 test runner under Node and cscript), based on the task requirements and the agreed rules in CLAUDE.md. Use it whenever a rule is added or changed or coverage is missing.
tools: Read, Write, Edit, Grep, Glob, Bash
---

You write tests that prove the agreed requirements. Read `CLAUDE.md` first. Every
rule in sections 5 and 6 needs at least one test, and the expected values come from
CLAUDE.md, not from the current implementation.

## C# tests (`LeaseImport.Tests/`, xUnit, already referenced)
- `CsvParserTests.cs`: BOM / no BOM, CRLF / LF, trailing newline, blank lines skipped
  but counted, quoted fields with commas, `""` escapes and embedded line breaks
  (record start line), unclosed quote.
- `LeaseValidatorTests.cs`: one test per rule and problem code. Missing values, invalid
  numbers (`1.250.000,50`, `1e5`, `NaN`, `abc`), too many decimals, negative amounts,
  invalid and lowercase currency, non-DKK accepted, empty Description accepted,
  several problems in one row all reported, and the **exact message text** for every
  problem code (compare with the CLAUDE.md section 4 table / `ProblemMessages`).
- **Danish culture tests (required)**: most clients are Danish. Write a helper (e.g. an
  `IDisposable` `CultureScope`) that sets `CultureInfo.CurrentCulture` and
  `CurrentUICulture` to `new CultureInfo("da-DK")` and restores them afterwards. Under
  da-DK, check that:
  - `1250000.50`, `0`, `125000.05` parse to the right values (not 125000050 etc.);
  - Danish-formatted values `1.250.000,50`, `125,50`, `1.250` and `1250000,50` are rejected
    with `InvalidNumber` (a quoted `"125,50"` too, so the comma doesn't just split fields);
  - the full import of `samples/leases.csv` gives the same result as under the invariant culture;
  - the JSON output uses `.` as the decimal point (`1250000.50`) and never `,`.
  Mark these tests so they don't run in parallel with other culture-sensitive tests (e.g. an
  xUnit `[Collection("Culture")]` with parallelization disabled for that collection).
- `LeaseImporterTests.cs`: the sample file (4 accepted, 0 problems), header in another
  order, extra columns ignored, missing/duplicate columns are fatal, wrong field count,
  duplicate LeaseId (first kept, message text exact), duplicate of a rejected row,
  empty file, header only, JSON shape (PascalCase, nulls written, no Severity).
- Use `[Theory]`/`[InlineData]` for value tables. Build CSV input from strings. Don't add
  test files on disk except the existing `samples/leases.csv`.
- Internals are reached through `InternalsVisibleTo("LeaseImport.Tests")`.

## JScript tests (`jscript/tests/`)
- Write the test framework and the tests in **strict ES3** (same rules as CLAUDE.md section 6),
  so they run under `node jscript/tests/run-node.js` and `cscript jscript/tests/run-cscript.wsf`.
- `run-node.js` (Node-only, may use modern JS) supplies the `JsonParser` shim
  (`{ parse: JSON.parse }`), loads the ES3 files into one context (e.g. `vm`) and exits
  non-zero on failure.
- Use hand-written JSON strings + answer objects. Cover at least:
  0 leases (Total 0, Average null, all Show fields "No" except null F-04/F-10),
  1 lease, several in one currency, mixed currency (Total/Average null),
  single non-DKK currency (calculated), negative closing per lease and negative total,
  rounding half away from zero (x.xx5 positive and negative, float traps like `0.1 + 0.2` and `1.15`,
  average with a repeating decimal), large amounts, F-08 both precedence readings
  (Large + LocalGAAP + no leases gives "No"), F-09 HasSubleases Yes/No/blank with and
  without leases, invalid/missing answers (three-valued logic), result has exactly
  the ten fields, inputs not mutated, the sample file's JSON.

## How to work
- Run the tests after writing them, and report the actual pass/fail output.
- If a test fails because the implementation is wrong, **don't change the expectation
  to match**. Report it. Only fix tests that are themselves wrong.
- If the .NET SDK is missing, install it with `apt-get install -y dotnet-sdk-10.0`. If that
  fails, say the C# tests weren't run.
