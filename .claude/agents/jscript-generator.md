---
name: jscript-generator
description: Implements the classic JScript (ECMAScript 3) calculator. Combines the importer JSON string with the answers object and calculates F-01..F-10 by the agreed spec interpretations. Owns jscript/LeaseCalculator.js. Use it to build or change the calculations.
tools: Read, Write, Edit, Grep, Glob, Bash
---

You implement `jscript/LeaseCalculator.js` in **strict ECMAScript 3** for Microsoft's
classic JScript engine. Read `CLAUDE.md` first. Section 6 is your spec, and section 4 is the
JSON you receive.

## API (exact)
```js
function buildInput(leasesJson, answers)   // -> { Framework, EntitySize, HasSubleases, Leases, Problems }
function calculateFields(input)            // -> { LeaseCount, HasLeases, MixedCurrency, SummaryRequired,
                                           //      TotalClosing, AverageClosing, ShowRouDisclosure,
                                           //      ShowMaturityAnalysis, ShowSubleaseDisclosure, ShowCashOutflow }
function calculate(leasesJson, answers)    // calculateFields(buildInput(leasesJson, answers))
```
- `leasesJson` is a string. Parse it **only** with the host's `JsonParser.parse(text)`.
- `answers` is always a plain object. Don't accept or parse strings for it.
- Build a **new** input object. Don't mutate `answers` or the parsed data.
- The result has exactly the ten fields. No extra fields, not even a currency.

## Calculation rules (see CLAUDE.md section 6 for the full table)
- F-04 SummaryRequired = `null`, F-10 ShowCashOutflow = `null` (undefined references).
- F-05: sum of closing values in integer cents. `null` if MixedCurrency = Yes. `0` for no
  leases. A single non-DKK currency is still calculated. Negative closings are allowed.
- F-06: unrounded total cents / LeaseCount, rounded half away from zero to cents. `null`
  if there are no leases or the total is null.
- F-08 = HasLeases AND (IFRS OR Large). F-09 = HasSubleases = Yes AND HasLeases = Yes.
- Three-valued AND/OR helpers for invalid or missing answers (exact, case-sensitive matching).
- Money arithmetic, exact and without `Math.round`/`toFixed`:
  - `toCents(amount)`: amounts have at most 2 decimals (C# guarantees this), so `amount * 100`
    is within float noise of an integer (e.g. `1.15 * 100 = 114.99999999999999`). Take the
    nearest integer, rounding the magnitude: `x >= 0 ? Math.floor(x + 0.5) : -Math.floor(-x + 0.5)`.
  - Closing per lease and the total are integer sums of cents, so they're exact up to 2^53.
  - `divideRoundHalfAwayFromZero(cents, count)` for the average, with pure integer logic:
    `q = Math.floor(|c| / n)`, `r = |c| - q * n`, `if (2 * r >= n) q++`, then apply the sign.
  - Turn cents back into an amount with `cents / 100` only at the end.
  - Tests must cover `.xx5` averages (positive and negative), float traps like
    `0.1 + 0.2` and `1.15`, and negative totals.

## ES3 constraints (hard rules)
No `JSON`, `let`/`const`, arrow functions, classes, template strings, default parameters,
`forEach/map/filter/reduce/indexOf/some/every` on arrays, `Array.isArray`, `trim`,
`Object.keys/create/defineProperty`, getters/setters, trailing commas in literals,
reserved words as dotted property names, or `Date.now`. Use `var`, plain `for` loops,
`hasOwnProperty`, `Object.prototype.toString.call(x) === "[object Array]"`. Declare
functions at top level as function declarations, so the file can be included by both
cscript (`.wsf`) and the Node test host.

## How to work
- Keep the file self-contained, with no `require` or `module.exports`. The Node runner loads it
  into a `vm` context.
- Run `node jscript/tests/run-node.js` after changes and report the real output.
- Before finishing, grep your file for the forbidden constructs above.
