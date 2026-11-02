# CLAUDE.md — Lease Import take-home exercise

Project memory. These decisions were **agreed with the user**. Do not change them
without asking. If code, a test or an agent disagrees with this file, this file wins.

## 1. The exercise

1. **C#**: read `leases.csv`, validate every value, write JSON with the
   accepted leases and the problems found.
2. **Classic JScript (ECMAScript 3)**: receive that JSON as a string together
   with the engagement answers (a plain object), combine them into one input
   object and calculate fields F-01 to F-10.
3. **Tests**: C# (xUnit) and JScript (hand-written JSON + answers, edge cases).
4. **Notes**: a short text file (`NOTES.md`) with thoughts, choices, spec
   problems and how each was handled, and AI usage.

Source documents: the brief (`Desktop_Developer_CaseView_Take-Home_Test_v14.docx`),
`samples/leases.csv`, and the user's JSON format proposal (section 4 below).

## 2. Repository layout

```
LeaseImportAssignment.slnx
LeaseImport/                 C# console app (net10.0, nullable enabled)
  Models.cs                  Lease, Problem, ImportResult (DTOs)
  CsvParser.cs               RFC 4180 tokenizer -> records with physical start line
  LeaseValidator.cs          one record -> Lease or list of Problems (value rules)
  LeaseImporter.cs           orchestration: header check, rows, duplicates, result
  Program.cs                 CLI: LeaseImport <input.csv> [output.json]
LeaseImport.Tests/           xUnit tests (CsvParserTests, LeaseValidatorTests, LeaseImporterTests)
samples/leases.csv           sample from the brief (UTF-8 with BOM, CRLF)
jscript/
  LeaseCalculator.js         ES3 calculator (the deliverable)
  tests/TestRunner.js        tiny ES3 assertion framework
  tests/LeaseCalculatorTests.js
  tests/run-node.js          Node host: JsonParser shim + loads the ES3 files
  tests/run-cscript.wsf      Windows Script Host runner (cscript) for the same tests
NOTES.md                     hand-in notes
```

C# classes are `internal`. Expose them to the tests with
`[assembly: InternalsVisibleTo("LeaseImport.Tests")]`. Don't make them public just for testing.

## 3. Environment

- The cloud container has **Node 22** and **no cscript**. The .NET SDK isn't preinstalled.
  `dot.net`/`dotnet-install.sh` is blocked by the network proxy. Install it with
  `apt-get install -y dotnet-sdk-10.0` (Ubuntu feed) instead. If installing fails, say so. Never claim C# tests pass without running them.
- Build/test: `dotnet test LeaseImportAssignment.slnx`
- JScript tests: `node jscript/tests/run-node.js`. On Windows also
  `cscript //nologo jscript/tests/run-cscript.wsf`.

## 4. JSON contract (C# output → JScript input)

```json
{
  "Leases": [
    { "Line": 2, "LeaseId": "L-2001", "AssetClass": "Property", "Description": "Office Aarhus",
      "Currency": "DKK", "Opening": 1250000.50, "Additions": 0, "Depreciation": 125000.05, "Disposals": 0 }
  ],
  "Problems": [
    { "Line": 5, "LeaseId": "L-2001", "Field": "LeaseId", "Code": "DuplicateLeaseId",
      "Message": "This lease can't be processed because its ID 'L-2001' is not unique (it already appears on line 2)." }
  ]
}
```

- PascalCase property names. Amounts are JSON numbers (C# `decimal`).
- `Problems[].LeaseId` is `null` when the id is missing or the row could not be split.
- `Problems[].Field` is `null` for problems that affect the whole row (e.g. wrong field count).
- Header and file-level problems use `Line: 1`.
- Null properties are written as `null`, not left out.
- **No `Severity`.** Every validation failure rejects the row. A rejected row
  never appears in `Leases`.
- **No extra fields** beyond this contract, in either the C# JSON or the JScript result.

### Problem codes and messages (use exactly these)

Every rule has its own message: plain language for a business user, plus the concrete
detail. Placeholders: `{field}` = column name, `{value}` = the trimmed raw value,
`{id}` = LeaseId, `{line}` = a line number, `{expected}`/`{actual}` = field counts.
Messages are built in one place (e.g. a `ProblemMessages` static class) so that tests
and code share the same text.

| Code | When | Message |
|---|---|---|
| `EmptyFile` | No header line at all (Line 1, Field null) | `The file can't be processed because it is empty (no header line was found).` |
| `MissingColumn` | Required header column missing (Line 1, Field = column name) | `The file can't be processed because the required column '{field}' is missing from the header.` |
| `DuplicateColumn` | Header column appears twice (Line 1, Field = column name) | `The file can't be processed because the column '{field}' appears more than once in the header.` |
| `UnclosedQuote` | Quoted field never closed (Line = record start line, Field null) | `This lease can't be processed because a quoted value starting on line {line} is never closed.` |
| `InvalidFieldCount` | Row field count ≠ header field count (Field null) | `This lease can't be processed because it has {actual} values, but the header has {expected} columns.` |
| `MissingValue` | Required field empty after trimming | `This lease can't be processed because '{field}' is empty.` |
| `InvalidNumber` | Not a valid invariant-culture decimal | `This lease can't be processed because '{field}' has the value '{value}', which is not a valid amount. Use digits with '.' as the decimal point and no thousands separators, e.g. 1250000.50.` |
| `TooManyDecimals` | More than 2 decimal places | `This lease can't be processed because '{field}' has the value '{value}', which has more than 2 decimals.` |
| `NegativeAmount` | Opening/Additions/Depreciation/Disposals < 0 | `This lease can't be processed because '{field}' has the value '{value}', which is negative. Amounts must be 0 or more.` |
| `InvalidCurrency` | Not an active ISO-4217 code (exact uppercase) | `This lease can't be processed because '{value}' is not a valid ISO-4217 currency code (use three uppercase letters, e.g. DKK).` |
| `DuplicateLeaseId` | LeaseId already seen on an earlier line | `This lease can't be processed because its ID '{id}' is not unique (it already appears on line {line}).` |

If a required column is missing or duplicated, the import is fatal: report the header
problems and accept **no** leases. A missing or unreadable input file is not JSON
output. It gives a non-zero exit code and a message on stderr.

## 5. C# rules (agreed, section B)

- **Encoding / lines**: UTF-8 with or without BOM. CRLF or LF. Line numbers are
  **physical** lines and the header is line 1. Skip blank lines (empty or whitespace-only), but they still count.
  A trailing newline is not a row.
- **Quoting**: full RFC 4180: `"..."` fields, `""` escapes, commas and line breaks
  inside quotes. A record's `Line` is the line where it **starts**. No `string.Split(',')`.
- **Header**: match columns by name (trimmed, case-sensitive), in any order. Ignore extra columns.
  Required: `LeaseId, AssetClass, Description, Currency, Opening, Additions, Depreciation, Disposals`.
- **Trimming**: trim every field value before validating it.
- **LeaseId**: required. Duplicates: the **first occurrence is kept**, later ones are rejected
  with `DuplicateLeaseId` (message in section 4; `{line}` = the first occurrence's line).
  Compare after trimming, case-sensitive. An id counts as seen even when its first row
  was rejected for another reason.
- **AssetClass**: required. No fixed list of values.
- **Description**: optional (may be empty).
- **Currency**: required, must be in a hard-coded list of active ISO-4217 codes, exact
  uppercase (`dkk` is rejected, not fixed). Non-DKK currencies are **valid**, because
  mixed currency is handled in JScript. Only circulating codes: fund, precious-metal, test
  and `XXX` codes and **retired codes** (BGN since 2026-01-01, HRK, ANG, SLL, ZWL, CUC) are
  rejected. Agreed: no grace period in code. NOTES.md mentions that recently retired codes
  (e.g. BGN) could get a 6-month grace period as a later special case.
- **Amounts** (Opening, Additions, Depreciation, Disposals): required (empty means
  `MissingValue`, never 0). `decimal` parsed with the invariant culture: `.` as the decimal
  point, optional leading sign (`+500.00` is accepted as 500.00; `-` is parsed so negatives
  can be reported as `NegativeAmount`), no
  thousands separators, no exponent, no NaN/Infinity, at most 2 decimals, and `>= 0`.
- **Danish culture (agreed)**: most clients are Danish. On a da-DK machine `.` groups
  thousands and `,` is the decimal point. Parsing and JSON output must **not** depend on
  `CurrentCulture`: always pass `CultureInfo.InvariantCulture`. Danish-formatted values
  are **rejected**, not accepted: `1.250.000,50`, `125,50` and `1250000,50` give `InvalidNumber`.
  `1.250` is a valid invariant number with 3 decimals, so it gives `TooManyDecimals`
  (agreed: no special case for Danish grouping).
  There must be tests that run with `CultureInfo.CurrentCulture`/`CurrentUICulture` set to
  `new CultureInfo("da-DK")` (restored afterwards) to prove that `1250000.50` still
  parses to 1250000.50, Danish-formatted values are rejected, and the JSON output keeps `.`
  as the decimal point (e.g. `1250000.50`, never `1250000,50`).
- **No closing value in C#.** C# doesn't calculate or check the closing value
  (including a negative closing). That belongs to the JScript part and its tests.
- **Report every problem in a row**, one Problem per field/rule. Don't stop at the first.
- An empty file with only a header is valid: 0 leases, 0 problems.
- Implementation choices (accepted): `DuplicateColumn` only for required columns (extra
  columns are ignored). Stray quotes RFC 4180 doesn't allow (`ab"c`) are kept literally. An
  unclosed quote in the header gives only `UnclosedQuote`. The decimal scale counts, so `0.000`
  gives `TooManyDecimals`. A row with `InvalidFieldCount` doesn't mark its id as seen. On a
  duplicate, the `DuplicateLeaseId` problem is listed first in that row.
- CLI exit codes: 0 = JSON written (also when it contains problems), 1 = usage error,
  2 = input missing/unreadable, 3 = output can't be written.
- Use `System.Text.Json`. No third-party CSV library.

## 6. JScript rules

### Language: strict ECMAScript 3 (classic JScript)
Not allowed: `JSON`, `let`/`const`, arrow functions, classes, template strings,
`Array.prototype.forEach/map/filter/reduce/indexOf`, `Array.isArray`,
`String.prototype.trim`, `Object.keys`, `Object.create`, getters/setters,
**trailing commas** in object/array literals, reserved words as property names with
dot access (`obj.class`, `obj.default`), `"use strict"`-dependent behaviour.
Use `var`, `for` loops, `typeof`, `Object.prototype.toString.call(x) === "[object Array]"`.

### API
```js
// leasesJson: string produced by the C# importer
// answers:    plain object { Framework, EntitySize, HasSubleases } — object only, never a string
function buildInput(leasesJson, answers)   // -> { Framework, EntitySize, HasSubleases, Leases, Problems }
function calculateFields(input)            // -> result object with F-01..F-10
function calculate(leasesJson, answers)    // = calculateFields(buildInput(leasesJson, answers))
```
- JSON is parsed **only** with the host object `JsonParser.parse(text)`.
- The input object contains exactly: `Framework`, `EntitySize`, `HasSubleases`
  (A-01..A-03), `Leases`, `Problems`. Problems are passed through and never used
  in calculations.
- The result object contains exactly the ten spec fields: `LeaseCount, HasLeases,
  MixedCurrency, SummaryRequired, TotalClosing, AverageClosing, ShowRouDisclosure,
  ShowMaturityAnalysis, ShowSubleaseDisclosure, ShowCashOutflow`. No extra fields.

### Answer values (exact match, case-sensitive)
- Framework: `"IFRS"` | `"LocalGAAP"`
- EntitySize: `"Micro"` | `"Small"` | `"Medium"` | `"Large"`
- HasSubleases: `"Yes"` | `"No"` | blank (`""`, `null` or missing)
- Anything else is **invalid** (e.g. `"ifrs"`, `"yes"`, missing Framework).

### Field definitions (agreed interpretations)

| ID | Field | Rule as implemented |
|---|---|---|
| F-01 | LeaseCount | Number of **accepted** leases (`Leases.length`). Rejected rows don't count. |
| F-02 | HasLeases | `"Yes"` if LeaseCount > 0, else `"No"` |
| F-03 | MixedCurrency | `"Yes"` if more than one distinct currency, else `"No"` (0 leases → `"No"`) |
| F-04 | SummaryRequired | **`null`**. `KpiRequired` doesn't exist (spec defect, decision 1). |
| F-05 | TotalClosing | Sum of (Opening + Additions − Depreciation − Disposals). `null` if MixedCurrency = Yes. **0 leases → `0`**. A single non-DKK currency is calculated as the spec says, with **no** currency field added (decision 5 changed). Negative closing values are allowed and simply summed. |
| F-06 | AverageClosing | TotalClosing / LeaseCount, from the **unrounded** total, rounded once. `null` if LeaseCount = 0 or TotalClosing is null. |
| F-07 | ShowRouDisclosure | HasLeases = Yes AND Framework = IFRS |
| F-08 | ShowMaturityAnalysis | **HasLeases = Yes AND (Framework = IFRS OR EntitySize = Large)** (decision 2) |
| F-09 | ShowSubleaseDisclosure | **HasSubleases = Yes AND HasLeases = Yes** (decision 3). HasSubleases = Yes with no leases → `"No"` (the inconsistency is documented in NOTES.md, with no output field). |
| F-10 | ShowCashOutflow | **`null`**. `TotalCashOutflow` doesn't exist (spec defect, decision 1). |

### Invalid / missing answers (decision 6): three-valued logic
- AND: `"No"` if any operand is `"No"`, else `null` if any operand is null/invalid, else `"Yes"`.
- OR: `"Yes"` if any operand is `"Yes"`, else `null` if any operand is null/invalid, else `"No"`.
- So F-07/F-08/F-09 are `"No"` whenever HasLeases = No, whatever the answers are.
- HasSubleases blank while leases exist means the question should have been answered,
  so it's unknown and F-09 is `null`.

### Output conventions
- Yes/No values are the strings `"Yes"` / `"No"`. A value that can't be calculated is `null`.
- Amounts are rounded to 2 decimals, **half away from zero**. Don't use `Math.round`
  or `toFixed` for money. Convert each input amount to integer cents (the nearest
  integer to `x * 100`, which is safe because inputs have at most 2 decimals), sum the
  cents as integers, and divide by 100 only at the end. The average uses integer
  division with a remainder: round half away from zero on `totalCents / count`.
- Amounts are returned as numbers, not strings.

## 7. Spec problems to report in NOTES.md
1. F-04 uses the non-existent `KpiRequired`, so it returns null.
2. F-10 uses the non-existent `TotalCashOutflow`, so it returns null.
3. F-08 is ambiguous because of operator precedence. We use `HasLeases AND (IFRS OR Large)`.
4. F-09 contradicts itself (HasSubleases is skipped when there are no leases). We use `HasLeases = Yes`.
5. F-06 divides by zero, and depends on F-05 being null. Both give null.
6. F-05 for 0 leases gives 0.
7. F-05 with a single currency other than the reporting currency (DKK) is calculated as
   specified, but it isn't in DKK. That's a business risk, documented and not changed.
8. The sample data contains EUR, so the sample gives MixedCurrency = Yes and Total/Average = null.
9. Totals cover accepted leases only. Rejected rows make the totals partial.
10. Invalid or missing answers aren't defined in the spec. We use three-valued logic.
11. Rounding: `Math.round` isn't half away from zero, and floating point needs integer cents.
12. Section numbering skips 5 in the brief.

## 8. Working conventions
- Agents live in `.claude/agents/`: `analyze-brief`, `code-reviewer`,
  `unit-tests-generator`, `json-importer`, `jscript-generator`.
- Keep code small and readable. Prefer clear names over comments.
- Every rule in sections 5 and 6 should have at least one test.
- Commit with clear messages on the designated branch. Don't add model identifiers to commits or code.
