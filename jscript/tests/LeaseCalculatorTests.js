// LeaseCalculatorTests.js - tests for LeaseCalculator.js, strict ECMAScript 3 (classic JScript).
// Expected values come from CLAUDE.md section 6 and hand calculation.
// The host loads TestRunner.js, LeaseCalculator.js and this file into one context, supplies
// JsonParser and a global Fixtures object ({ leases, leasesWithProblems } with the JSON written by
// the C# importer, plus { leasesValidDkk, leasesMixed, leasesAllEur, leasesUnusualText } from the
// hand-written samples/expected),
// then calls TestRunner.run(writeLine).

(function () {
    var T = TestRunner;

    var INPUT_KEYS = ["Framework", "EntitySize", "HasSubleases", "Leases", "Problems"];
    var RESULT_KEYS = ["LeaseCount", "HasLeases", "MixedCurrency", "SummaryRequired", "TotalClosing",
        "AverageClosing", "ShowRouDisclosure", "ShowMaturityAnalysis", "ShowSubleaseDisclosure",
        "ShowCashOutflow"];

    // -----------------------------------------------------------------------
    // Helpers (JSON is always built as text, never with a JSON object)
    // -----------------------------------------------------------------------

    // One lease as JSON text. Amounts are passed as JSON number literals (strings), e.g. "1250000.50".
    function lease(id, currency, opening, additions, depreciation, disposals) {
        return "{\"Line\": 2, \"LeaseId\": \"" + id + "\", \"AssetClass\": \"Property\", "
            + "\"Description\": \"\", \"Currency\": \"" + currency + "\", "
            + "\"Opening\": " + opening + ", \"Additions\": " + additions + ", "
            + "\"Depreciation\": " + depreciation + ", \"Disposals\": " + disposals + "}";
    }

    // A DKK lease whose closing value is `closing` (closing >= 0 goes to Opening, else to Depreciation).
    function dkkClosing(id, closing) {
        if (closing.charAt(0) === "-") {
            return lease(id, "DKK", "0", "0", closing.substring(1), "0");
        }
        return lease(id, "DKK", closing, "0", "0", "0");
    }

    function problem(line, id, field, code, message) {
        return "{\"Line\": " + line + ", \"LeaseId\": " + (id === null ? "null" : "\"" + id + "\"")
            + ", \"Field\": " + (field === null ? "null" : "\"" + field + "\"")
            + ", \"Code\": \"" + code + "\", \"Message\": \"" + message + "\"}";
    }

    function doc(leases, problems) {
        return "{\"Leases\": [" + leases.join(", ") + "], \"Problems\": [" + (problems || []).join(", ") + "]}";
    }

    function answers(framework, entitySize, hasSubleases) {
        return { Framework: framework, EntitySize: entitySize, HasSubleases: hasSubleases };
    }

    // Builds the full expected result: defaults for a run with leases, then the overrides.
    function expected(overrides) {
        var result = {
            LeaseCount: 0, HasLeases: "No", MixedCurrency: "No", SummaryRequired: null,
            TotalClosing: 0, AverageClosing: null, ShowRouDisclosure: "No",
            ShowMaturityAnalysis: "No", ShowSubleaseDisclosure: "No", ShowCashOutflow: null
        };
        for (var key in overrides) {
            if (Object.prototype.hasOwnProperty.call(overrides, key)) {
                result[key] = overrides[key];
            }
        }
        return result;
    }

    function amounts(json) {
        var r = calculate(json, answers("IFRS", "Large", "Yes"));
        return { total: r.TotalClosing, average: r.AverageClosing };
    }

    function checkAmounts(json, total, average, message) {
        var a = amounts(json);
        T.equal(a.total, total, (message || "") + " TotalClosing");
        T.equal(a.average, average, (message || "") + " AverageClosing");
    }

    function repeat(text, count) {
        var parts = [];
        for (var i = 0; i < count; i++) {
            parts[parts.length] = text;
        }
        return parts;
    }

    function concat(a, b) {
        var all = [];
        var i;
        for (i = 0; i < a.length; i++) {
            all[all.length] = a[i];
        }
        for (i = 0; i < b.length; i++) {
            all[all.length] = b[i];
        }
        return all;
    }

    var NO_LEASES = doc([]);
    var ONE_LEASE = doc([lease("L-2001", "DKK", "1250000.50", "0", "125000.05", "0")]);

    // samples/leases.csv as hand-written JSON (closing: 1125000.45, 444000.00, 15000.00, EUR 270000.00).
    var SAMPLE_JSON = "{\"Leases\": ["
        + "{\"Line\": 2, \"LeaseId\": \"L-2001\", \"AssetClass\": \"Property\", \"Description\": \"Office Aarhus\", "
        + "\"Currency\": \"DKK\", \"Opening\": 1250000.50, \"Additions\": 0, \"Depreciation\": 125000.05, \"Disposals\": 0}, "
        + "{\"Line\": 3, \"LeaseId\": \"L-2002\", \"AssetClass\": \"Vehicles\", \"Description\": \"Fleet (12 cars)\", "
        + "\"Currency\": \"DKK\", \"Opening\": 480000.00, \"Additions\": 60000.00, \"Depreciation\": 96000.00, \"Disposals\": 0}, "
        + "{\"Line\": 4, \"LeaseId\": \"L-2003\", \"AssetClass\": \"Equipment\", \"Description\": \"Printer\", "
        + "\"Currency\": \"DKK\", \"Opening\": 20000.00, \"Additions\": 0, \"Depreciation\": 5000.00, \"Disposals\": 0}, "
        + "{\"Line\": 5, \"LeaseId\": \"L-2004\", \"AssetClass\": \"Property\", \"Description\": \"Warehouse Odense\", "
        + "\"Currency\": \"EUR\", \"Opening\": 300000.00, \"Additions\": 0, \"Depreciation\": 30000.00, \"Disposals\": 0}"
        + "], \"Problems\": []}";

    // The three DKK leases of the sample: 1125000.45 + 444000.00 + 15000.00 = 1584000.45, / 3 = 528000.15.
    var SAMPLE_DKK_ONLY = doc([
        lease("L-2001", "DKK", "1250000.50", "0", "125000.05", "0"),
        lease("L-2002", "DKK", "480000.00", "60000.00", "96000.00", "0"),
        lease("L-2003", "DKK", "20000.00", "0", "5000.00", "0")
    ]);

    // -----------------------------------------------------------------------
    T.group("buildInput");
    // -----------------------------------------------------------------------

    T.test("input object has exactly the five keys and the answer values", function () {
        var input = buildInput(ONE_LEASE, answers("IFRS", "Medium", "No"));
        T.hasExactKeys(input, INPUT_KEYS);
        T.equal(input.Framework, "IFRS");
        T.equal(input.EntitySize, "Medium");
        T.equal(input.HasSubleases, "No");
        T.equal(input.Leases.length, 1);
        T.equal(input.Leases[0].LeaseId, "L-2001");
        T.equal(input.Leases[0].Opening, 1250000.5);
        T.equal(input.Problems.length, 0);
    });

    T.test("input object has the five keys even when answers are missing", function () {
        T.hasExactKeys(buildInput(NO_LEASES, {}), INPUT_KEYS);
    });

    T.test("Problems are passed through unchanged", function () {
        var json = doc([], [problem(5, "L-2001", "LeaseId", "DuplicateLeaseId", "dup"),
            problem(1, null, null, "EmptyFile", "empty")]);
        var input = buildInput(json, answers("IFRS", "Large", "No"));
        T.equal(input.Problems.length, 2);
        T.equal(input.Problems[0].Code, "DuplicateLeaseId");
        T.equal(input.Problems[0].Line, 5);
        T.equal(input.Problems[1].LeaseId, null);
        T.equal(input.Problems[1].Field, null);
    });

    T.test("calculate equals calculateFields(buildInput(...))", function () {
        var a = answers("LocalGAAP", "Large", "Yes");
        T.equalFields(calculate(SAMPLE_DKK_ONLY, a), calculateFields(buildInput(SAMPLE_DKK_ONLY, a)));
    });

    // -----------------------------------------------------------------------
    T.group("Broken input throws");
    // -----------------------------------------------------------------------

    T.test("leasesJson that is not a string throws an Error", function () {
        var a = answers("IFRS", "Large", "No");
        T.assertThrowsError(function () { buildInput(null, a); }, "null");
        T.assertThrowsError(function () { buildInput(undefined, a); }, "undefined");
        T.assertThrowsError(function () { buildInput(42, a); }, "number");
        T.assertThrowsError(function () { buildInput({ Leases: [], Problems: [] }, a); }, "object");
        T.assertThrowsError(function () { calculate({ Leases: [], Problems: [] }, a); }, "calculate with object");
    });

    T.test("answers that are not a plain object throw an Error", function () {
        T.assertThrowsError(function () { buildInput(NO_LEASES, null); }, "null");
        T.assertThrowsError(function () { buildInput(NO_LEASES, undefined); }, "undefined");
        T.assertThrowsError(function () { buildInput(NO_LEASES, "IFRS"); }, "string");
        T.assertThrowsError(function () { buildInput(NO_LEASES, "{\"Framework\": \"IFRS\"}"); }, "JSON string");
        T.assertThrowsError(function () { buildInput(NO_LEASES, ["IFRS", "Large", "No"]); }, "array");
        T.assertThrowsError(function () { buildInput(NO_LEASES, 1); }, "number");
        T.assertThrowsError(function () { calculate(NO_LEASES, "IFRS"); }, "calculate with string");
    });

    T.test("parsed JSON that is not an object throws an Error", function () {
        var a = answers("IFRS", "Large", "No");
        T.assertThrowsError(function () { buildInput("[]", a); }, "array");
        T.assertThrowsError(function () { buildInput("null", a); }, "null");
        T.assertThrowsError(function () { buildInput("42", a); }, "number");
        T.assertThrowsError(function () { buildInput("\"text\"", a); }, "string");
        T.assertThrowsError(function () { buildInput("true", a); }, "boolean");
    });

    T.test("Leases that is not an array throws an Error", function () {
        var a = answers("IFRS", "Large", "No");
        T.assertThrowsError(function () { buildInput("{\"Problems\": []}", a); }, "missing");
        T.assertThrowsError(function () { buildInput("{\"Leases\": null, \"Problems\": []}", a); }, "null");
        T.assertThrowsError(function () { buildInput("{\"Leases\": {}, \"Problems\": []}", a); }, "object");
        T.assertThrowsError(function () { buildInput("{\"Leases\": \"x\", \"Problems\": []}", a); }, "string");
    });

    T.test("Problems that is not an array throws an Error", function () {
        var a = answers("IFRS", "Large", "No");
        T.assertThrowsError(function () { buildInput("{\"Leases\": []}", a); }, "missing");
        T.assertThrowsError(function () { buildInput("{\"Leases\": [], \"Problems\": null}", a); }, "null");
        T.assertThrowsError(function () { buildInput("{\"Leases\": [], \"Problems\": {}}", a); }, "object");
        T.assertThrowsError(function () { buildInput("{\"Leases\": [], \"Problems\": 0}", a); }, "number");
    });

    T.test("an amount that is not a finite number throws an Error", function () {
        var a = answers("IFRS", "Large", "No");
        T.assertThrowsError(function () { calculate(doc([lease("L-1", "DKK", "\"100.00\"", "0", "0", "0")]), a); },
            "Opening as string");
        T.assertThrowsError(function () { calculate(doc([lease("L-1", "DKK", "0", "null", "0", "0")]), a); },
            "Additions null");
        T.assertThrowsError(function () { calculate(doc([lease("L-1", "DKK", "0", "0", "true", "0")]), a); },
            "Depreciation boolean");
        T.assertThrowsError(function () { calculate(doc([lease("L-1", "DKK", "0", "0", "0", "1e400")]), a); },
            "Disposals Infinity (1e400)");
        T.assertThrowsError(function () {
            calculate("{\"Leases\": [{\"LeaseId\": \"L-1\", \"Currency\": \"DKK\", \"Additions\": 0, "
                + "\"Depreciation\": 0, \"Disposals\": 0}], \"Problems\": []}", a);
        }, "Opening missing");
    });

    T.test("an amount that is not a finite number throws an Error also with mixed currency", function () {
        // Total/Average are null for mixed currency, but the broken amount must still be an error.
        var json = doc([lease("L-1", "DKK", "100.00", "0", "0", "0"), lease("L-2", "EUR", "\"abc\"", "0", "0", "0")]);
        T.assertThrowsError(function () { calculate(json, answers("IFRS", "Large", "No")); });
    });

    T.test("invalid JSON lets the parser exception through", function () {
        var a = answers("IFRS", "Large", "No");
        T.assertThrows(function () { buildInput("{", a); }, "truncated");
        T.assertThrows(function () { buildInput("", a); }, "empty string");
        T.assertThrows(function () { buildInput("{Leases: []}", a); }, "unquoted key");
    });

    T.test("valid answer objects with invalid values are not errors", function () {
        T.assertNoThrow(function () { calculate(ONE_LEASE, { Framework: 1, EntitySize: true, HasSubleases: {} }); });
        T.assertNoThrow(function () { calculate(ONE_LEASE, answers("ifrs", "large", "yes")); });
        T.assertNoThrow(function () { calculate(ONE_LEASE, {}); });
    });

    // -----------------------------------------------------------------------
    T.group("Lease counts and currency (F-01..F-03)");
    // -----------------------------------------------------------------------

    T.test("0 leases: Total 0, Average null, Show fields No, F-04/F-10 null", function () {
        T.equalFields(calculate(NO_LEASES, answers("IFRS", "Large", "No")), expected({}));
    });

    T.test("0 leases with HasSubleases Yes: same, except ShowSubleaseDisclosure null (contradiction)", function () {
        T.equalFields(calculate(NO_LEASES, answers("IFRS", "Large", "Yes")), expected({ ShowSubleaseDisclosure: null }));
    });

    T.test("1 lease", function () {
        T.equalFields(calculate(ONE_LEASE, answers("IFRS", "Small", "No")), expected({
            LeaseCount: 1, HasLeases: "Yes", TotalClosing: 1125000.45, AverageClosing: 1125000.45,
            ShowRouDisclosure: "Yes", ShowMaturityAnalysis: "Yes", ShowSubleaseDisclosure: "No"
        }));
    });

    T.test("several leases in one currency", function () {
        T.equalFields(calculate(SAMPLE_DKK_ONLY, answers("LocalGAAP", "Medium", "Yes")), expected({
            LeaseCount: 3, HasLeases: "Yes", TotalClosing: 1584000.45, AverageClosing: 528000.15,
            ShowRouDisclosure: "No", ShowMaturityAnalysis: "No", ShowSubleaseDisclosure: "Yes"
        }));
    });

    T.test("mixed currency: MixedCurrency Yes, Total and Average null", function () {
        var json = doc([dkkClosing("L-1", "100.00"), lease("L-2", "EUR", "50.00", "0", "0", "0")]);
        T.equalFields(calculate(json, answers("IFRS", "Medium", "No")), expected({
            LeaseCount: 2, HasLeases: "Yes", MixedCurrency: "Yes", TotalClosing: null, AverageClosing: null,
            ShowRouDisclosure: "Yes", ShowMaturityAnalysis: "Yes", ShowSubleaseDisclosure: "No"
        }));
    });

    T.test("mixed currency where the different currency is not the first or last lease", function () {
        var json = doc([dkkClosing("L-1", "1.00"), lease("L-2", "SEK", "1.00", "0", "0", "0"), dkkClosing("L-3", "1.00")]);
        var r = calculate(json, answers("IFRS", "Medium", "No"));
        T.equal(r.MixedCurrency, "Yes");
        T.equal(r.TotalClosing, null);
        T.equal(r.AverageClosing, null);
    });

    T.test("single non-DKK currency is calculated, with no currency field added", function () {
        // 300000.00 - 30000.00 = 270000.00; 1000.00 + 500.00 - 250.00 - 50.00 = 1200.00
        var json = doc([lease("L-1", "EUR", "300000.00", "0", "30000.00", "0"),
            lease("L-2", "EUR", "1000.00", "500.00", "250.00", "50.00")]);
        T.equalFields(calculate(json, answers("IFRS", "Micro", "No")), expected({
            LeaseCount: 2, HasLeases: "Yes", MixedCurrency: "No", TotalClosing: 271200, AverageClosing: 135600,
            ShowRouDisclosure: "Yes", ShowMaturityAnalysis: "Yes", ShowSubleaseDisclosure: "No"
        }));
    });

    T.test("rejected rows (Problems) are not counted and not used", function () {
        var problems = [problem(3, "L-9", "Currency", "InvalidCurrency", "x"),
            problem(4, "L-1", "LeaseId", "DuplicateLeaseId", "y")];
        var a = answers("IFRS", "Large", "Yes");
        var withProblems = calculate(doc([dkkClosing("L-1", "10.00")], problems), a);
        var withoutProblems = calculate(doc([dkkClosing("L-1", "10.00")]), a);
        T.equal(withProblems.LeaseCount, 1);
        T.equal(withProblems.TotalClosing, 10);
        T.equalFields(withProblems, withoutProblems);
    });

    T.test("only Problems and no leases gives the 0-lease result", function () {
        var json = doc([], [problem(2, "L-1", "Opening", "InvalidNumber", "x")]);
        T.equalFields(calculate(json, answers("IFRS", "Large", "No")), expected({}), "HasSubleases No");
        T.equalFields(calculate(json, answers("IFRS", "Large", "Yes")), expected({ ShowSubleaseDisclosure: null }),
            "HasSubleases Yes (contradiction -> F-09 null)");
    });

    // -----------------------------------------------------------------------
    T.group("Spec defects F-04 / F-10");
    // -----------------------------------------------------------------------

    T.test("SummaryRequired and ShowCashOutflow are always null", function () {
        var docs = [NO_LEASES, ONE_LEASE, SAMPLE_JSON];
        var answerSets = [answers("IFRS", "Large", "Yes"), answers("LocalGAAP", "Micro", "No"), {}];
        for (var d = 0; d < docs.length; d++) {
            for (var s = 0; s < answerSets.length; s++) {
                var r = calculate(docs[d], answerSets[s]);
                T.equal(r.SummaryRequired, null, "SummaryRequired doc " + d + " answers " + s);
                T.equal(r.ShowCashOutflow, null, "ShowCashOutflow doc " + d + " answers " + s);
            }
        }
    });

    // -----------------------------------------------------------------------
    T.group("Closing totals (F-05 / F-06)");
    // -----------------------------------------------------------------------

    T.test("negative closing value of a lease is summed", function () {
        // 100.00 - 150.00 - 25.50 = -75.50; + 200.00 = 124.50; / 2 = 62.25
        var json = doc([lease("L-1", "DKK", "100.00", "0", "150.00", "25.50"), dkkClosing("L-2", "200.00")]);
        checkAmounts(json, 124.5, 62.25);
    });

    T.test("negative total and negative average", function () {
        // 0 - 10.00 - 0.01 = -10.01; + 5.00 = -5.01; / 2 = -2.505 -> -2.51
        var json = doc([lease("L-1", "DKK", "0", "0", "10.00", "0.01"), dkkClosing("L-2", "5.00")]);
        checkAmounts(json, -5.01, -2.51);
    });

    T.test("total of exactly zero is 0, not -0", function () {
        var json = doc([dkkClosing("L-1", "-12.34"), dkkClosing("L-2", "12.34")]);
        checkAmounts(json, 0, 0);
    });

    T.test("all four amounts are used: Opening + Additions - Depreciation - Disposals", function () {
        // 1000.00 + 200.00 - 300.00 - 50.00 = 850.00
        checkAmounts(doc([lease("L-1", "DKK", "1000.00", "200.00", "300.00", "50.00")]), 850, 850);
    });

    T.test("amounts written as 0, 0.00 and 0.0 (as C# writes decimals) give 0", function () {
        checkAmounts(doc([lease("L-1", "DKK", "0", "0.00", "0", "0.0")]), 0, 0);
    });

    // -----------------------------------------------------------------------
    T.group("Rounding (half away from zero, integer cents)");
    // -----------------------------------------------------------------------

    T.test("0.1 + 0.2 is exactly 0.3", function () {
        checkAmounts(doc([lease("L-1", "DKK", "0.1", "0.2", "0", "0")]), 0.3, 0.3);
    });

    T.test("1.1 + 2.2 is exactly 3.3 and 4.35 + 0.07 is exactly 4.42", function () {
        checkAmounts(doc([lease("L-1", "DKK", "1.1", "2.2", "0", "0")]), 3.3, 3.3, "1.1+2.2");
        checkAmounts(doc([lease("L-1", "DKK", "4.35", "0.07", "0", "0")]), 4.42, 4.42, "4.35+0.07");
    });

    T.test("1.15 stays 1.15 (1.15 * 100 = 114.99999999999999)", function () {
        checkAmounts(doc([dkkClosing("L-1", "1.15")]), 1.15, 1.15);
        // 1.15 - 0.15 = 1.00
        checkAmounts(doc([lease("L-1", "DKK", "1.15", "0", "0.15", "0")]), 1, 1, "1.15-0.15");
    });

    T.test("average 1.15 / 2 = 0.575 rounds to 0.58", function () {
        checkAmounts(doc([dkkClosing("L-1", "1.15"), dkkClosing("L-2", "0")]), 1.15, 0.58);
    });

    T.test("average x.xx5 positive rounds up: 0.005 -> 0.01, 0.615 -> 0.62, 1.255 -> 1.26", function () {
        checkAmounts(doc([dkkClosing("L-1", "0.01"), dkkClosing("L-2", "0")]), 0.01, 0.01, "0.01/2");
        checkAmounts(doc([dkkClosing("L-1", "1.23"), dkkClosing("L-2", "0")]), 1.23, 0.62, "1.23/2");
        checkAmounts(doc([dkkClosing("L-1", "1.25"), dkkClosing("L-2", "1.26")]), 2.51, 1.26, "2.51/2");
    });

    T.test("average x.xx5 negative rounds away from zero: -0.005 -> -0.01, -0.615 -> -0.62, -1.255 -> -1.26", function () {
        checkAmounts(doc([dkkClosing("L-1", "-0.01"), dkkClosing("L-2", "0")]), -0.01, -0.01, "-0.01/2");
        checkAmounts(doc([dkkClosing("L-1", "-1.23"), dkkClosing("L-2", "0")]), -1.23, -0.62, "-1.23/2");
        checkAmounts(doc([dkkClosing("L-1", "-1.25"), dkkClosing("L-2", "-1.26")]), -2.51, -1.26, "-2.51/2");
    });

    T.test("average below and above half: 0.04 / 3 -> 0.01, 0.05 / 3 -> 0.02", function () {
        checkAmounts(doc([dkkClosing("L-1", "0.04"), dkkClosing("L-2", "0"), dkkClosing("L-3", "0")]), 0.04, 0.01);
        checkAmounts(doc([dkkClosing("L-1", "0.05"), dkkClosing("L-2", "0"), dkkClosing("L-3", "0")]), 0.05, 0.02);
    });

    T.test("average with a repeating decimal: 100/3 -> 33.33, 200/3 -> 66.67, -200/3 -> -66.67, 10/7 -> 1.43", function () {
        checkAmounts(doc([dkkClosing("L-1", "100.00"), dkkClosing("L-2", "0"), dkkClosing("L-3", "0")]),
            100, 33.33, "100/3");
        checkAmounts(doc([dkkClosing("L-1", "200.00"), dkkClosing("L-2", "0"), dkkClosing("L-3", "0")]),
            200, 66.67, "200/3");
        checkAmounts(doc([dkkClosing("L-1", "-200.00"), dkkClosing("L-2", "0"), dkkClosing("L-3", "0")]),
            -200, -66.67, "-200/3");
        checkAmounts(doc(concat([dkkClosing("L-1", "10.00")], repeat(dkkClosing("L-x", "0"), 6))),
            10, 1.43, "10/7");
    });

    T.test("average is calculated from the unrounded total", function () {
        // 0.01 + 0.01 + 0.02 = 0.04 -> 0.04 / 3 = 0.01333 -> 0.01
        // 3 x 33.33 + 0.02 = 100.01 -> / 4 = 25.0025 -> 25.00
        checkAmounts(doc([dkkClosing("L-1", "0.01"), dkkClosing("L-2", "0.01"), dkkClosing("L-3", "0.02")]),
            0.04, 0.01, "0.04/3");
        checkAmounts(doc(concat(repeat(dkkClosing("L-x", "33.33"), 3), [dkkClosing("L-4", "0.02")])),
            100.01, 25, "100.01/4");
    });

    T.test("amounts are returned as numbers", function () {
        var r = calculate(ONE_LEASE, answers("IFRS", "Large", "No"));
        T.equal(typeof r.TotalClosing, "number");
        T.equal(typeof r.AverageClosing, "number");
        T.equal(typeof r.LeaseCount, "number");
    });

    // -----------------------------------------------------------------------
    T.group("Large amounts");
    // -----------------------------------------------------------------------

    T.test("a single large amount: 9999999999.99", function () {
        checkAmounts(doc([dkkClosing("L-1", "9999999999.99")]), 9999999999.99, 9999999999.99);
    });

    T.test("two large amounts: total 19999999999.97, average 9999999999.985 -> 9999999999.99", function () {
        checkAmounts(doc([dkkClosing("L-1", "9999999999.99"), dkkClosing("L-2", "9999999999.98")]),
            19999999999.97, 9999999999.99);
    });

    T.test("a large negative closing: -9999999999.99", function () {
        checkAmounts(doc([dkkClosing("L-1", "-9999999999.99")]), -9999999999.99, -9999999999.99);
    });

    // -----------------------------------------------------------------------
    T.group("Volume and precision (10,000 leases)");
    // -----------------------------------------------------------------------
    // Every amount and every total below stays within 2^53 = 9,007,199,254,740,992 cents.
    // The largest total in cents is 10,000 x 9,999,999,999 = 99,999,999,990,000 (about 1e14),
    // well below 2^53, so integer cents are exact.

    T.test("10,000 x 0.01: total 100.00, average 0.01", function () {
        var json = doc(repeat(dkkClosing("L-x", "0.01"), 10000));
        var r = calculate(json, answers("IFRS", "Large", "No"));
        T.equal(r.LeaseCount, 10000);
        T.equal(r.TotalClosing, 100);
        T.equal(r.AverageClosing, 0.01);
    });

    T.test("10,000 x (0.1 + 0.2): total 3000.00, average 0.30", function () {
        checkAmounts(doc(repeat(lease("L-x", "DKK", "0.1", "0.2", "0", "0"), 10000)), 3000, 0.3);
    });

    T.test("10,000 x 99999999.99: total 999999999900.00 (99,999,999,990,000 cents), average 99999999.99", function () {
        checkAmounts(doc(repeat(dkkClosing("L-x", "99999999.99"), 10000)), 999999999900, 99999999.99);
    });

    T.test("9,999 x 0.01 + 1 x 99999999.99: total 100000099.98, average 10000.009998 -> 10000.01", function () {
        // 9,999 + 9,999,999,999 = 10,000,009,998 cents; / 10,000 = 1,000,000.9998 cents -> 1,000,001 cents
        var json = doc(concat(repeat(dkkClosing("L-x", "0.01"), 9999), [dkkClosing("L-big", "99999999.99")]));
        checkAmounts(json, 100000099.98, 10000.01);
    });

    T.test("5,000 x 0.10 and 5,000 x -0.03: total 350.00, average 0.035 -> 0.04", function () {
        var json = doc(concat(repeat(dkkClosing("L-p", "0.10"), 5000), repeat(dkkClosing("L-n", "-0.03"), 5000)));
        checkAmounts(json, 350, 0.04);
    });

    T.test("5,000 x -0.10 and 5,000 x 0.03: total -350.00, average -0.035 -> -0.04", function () {
        var json = doc(concat(repeat(dkkClosing("L-n", "-0.10"), 5000), repeat(dkkClosing("L-p", "0.03"), 5000)));
        checkAmounts(json, -350, -0.04);
    });

    T.test("10,000 x (99999999.99 + 0.01 - 0.01 - 0.01): total 999999999800.00, average 99999999.98", function () {
        // per lease 9,999,999,999 + 1 - 1 - 1 = 9,999,999,998 cents
        checkAmounts(doc(repeat(lease("L-x", "DKK", "99999999.99", "0.01", "0.01", "0.01"), 10000)),
            999999999800, 99999999.98);
    });

    // -----------------------------------------------------------------------
    T.group("ShowRouDisclosure (F-07)");
    // -----------------------------------------------------------------------

    T.test("leases + IFRS -> Yes, leases + LocalGAAP -> No", function () {
        T.equal(calculate(ONE_LEASE, answers("IFRS", "Small", "No")).ShowRouDisclosure, "Yes");
        T.equal(calculate(ONE_LEASE, answers("LocalGAAP", "Large", "No")).ShowRouDisclosure, "No");
    });

    T.test("no leases -> No whatever the Framework", function () {
        T.equal(calculate(NO_LEASES, answers("IFRS", "Large", "Yes")).ShowRouDisclosure, "No", "IFRS");
        T.equal(calculate(NO_LEASES, answers("ifrs", "x", "x")).ShowRouDisclosure, "No", "invalid");
        T.equal(calculate(NO_LEASES, {}).ShowRouDisclosure, "No", "missing");
    });

    T.test("leases + invalid or missing Framework -> null", function () {
        T.equal(calculate(ONE_LEASE, answers("ifrs", "Large", "No")).ShowRouDisclosure, null, "ifrs");
        T.equal(calculate(ONE_LEASE, answers("IFRS ", "Large", "No")).ShowRouDisclosure, null, "trailing space");
        T.equal(calculate(ONE_LEASE, answers(1, "Large", "No")).ShowRouDisclosure, null, "number");
        T.equal(calculate(ONE_LEASE, answers(null, "Large", "No")).ShowRouDisclosure, null, "null");
        T.equal(calculate(ONE_LEASE, { EntitySize: "Large", HasSubleases: "No" }).ShowRouDisclosure, null, "missing");
    });

    // -----------------------------------------------------------------------
    T.group("ShowMaturityAnalysis (F-08)");
    // -----------------------------------------------------------------------

    T.test("leases: IFRS or Large -> Yes, otherwise No", function () {
        T.equal(calculate(ONE_LEASE, answers("IFRS", "Small", "No")).ShowMaturityAnalysis, "Yes", "IFRS Small");
        T.equal(calculate(ONE_LEASE, answers("IFRS", "Large", "No")).ShowMaturityAnalysis, "Yes", "IFRS Large");
        T.equal(calculate(ONE_LEASE, answers("LocalGAAP", "Large", "No")).ShowMaturityAnalysis, "Yes", "LocalGAAP Large");
        T.equal(calculate(ONE_LEASE, answers("LocalGAAP", "Medium", "No")).ShowMaturityAnalysis, "No", "LocalGAAP Medium");
        T.equal(calculate(ONE_LEASE, answers("LocalGAAP", "Micro", "No")).ShowMaturityAnalysis, "No", "LocalGAAP Micro");
    });

    T.test("precedence: Large + LocalGAAP + no leases -> No (HasLeases AND (IFRS OR Large))", function () {
        // The other reading, (HasLeases AND IFRS) OR Large, would give Yes here.
        T.equal(calculate(NO_LEASES, answers("LocalGAAP", "Large", "No")).ShowMaturityAnalysis, "No");
    });

    T.test("precedence: IFRS + no leases -> No under both readings", function () {
        T.equal(calculate(NO_LEASES, answers("IFRS", "Small", "No")).ShowMaturityAnalysis, "No");
    });

    T.test("three-valued OR inside the AND", function () {
        T.equal(calculate(ONE_LEASE, answers("ifrs", "Large", "No")).ShowMaturityAnalysis, "Yes", "invalid fw + Large");
        T.equal(calculate(ONE_LEASE, answers("IFRS", "large", "No")).ShowMaturityAnalysis, "Yes", "IFRS + invalid size");
        T.equal(calculate(ONE_LEASE, answers("ifrs", "Medium", "No")).ShowMaturityAnalysis, null, "invalid fw + Medium");
        T.equal(calculate(ONE_LEASE, answers("LocalGAAP", "large", "No")).ShowMaturityAnalysis, null, "LocalGAAP + invalid size");
        T.equal(calculate(ONE_LEASE, {}).ShowMaturityAnalysis, null, "both missing");
        T.equal(calculate(NO_LEASES, answers("ifrs", "large", "No")).ShowMaturityAnalysis, "No", "no leases, both invalid");
    });

    // -----------------------------------------------------------------------
    T.group("ShowSubleaseDisclosure (F-09)");
    // -----------------------------------------------------------------------

    T.test("with leases: Yes -> Yes, No -> No", function () {
        T.equal(calculate(ONE_LEASE, answers("IFRS", "Large", "Yes")).ShowSubleaseDisclosure, "Yes");
        T.equal(calculate(ONE_LEASE, answers("IFRS", "Large", "No")).ShowSubleaseDisclosure, "No");
    });

    T.test("with leases: blank (\"\", null, missing) -> null", function () {
        T.equal(calculate(ONE_LEASE, answers("IFRS", "Large", "")).ShowSubleaseDisclosure, null, "empty string");
        T.equal(calculate(ONE_LEASE, answers("IFRS", "Large", null)).ShowSubleaseDisclosure, null, "null");
        T.equal(calculate(ONE_LEASE, { Framework: "IFRS", EntitySize: "Large" }).ShowSubleaseDisclosure, null, "missing");
    });

    T.test("with leases: invalid value -> null", function () {
        T.equal(calculate(ONE_LEASE, answers("IFRS", "Large", "yes")).ShowSubleaseDisclosure, null, "yes");
        T.equal(calculate(ONE_LEASE, answers("IFRS", "Large", true)).ShowSubleaseDisclosure, null, "true");
    });

    T.test("without leases: Yes -> null (contradicting answers, CLAUDE.md F-09 special case)", function () {
        T.equal(calculate(NO_LEASES, answers("IFRS", "Large", "Yes")).ShowSubleaseDisclosure, null, "IFRS Large");
        T.equal(calculate(NO_LEASES, answers("LocalGAAP", "Micro", "Yes")).ShowSubleaseDisclosure, null, "LocalGAAP Micro");
        T.equal(calculate(NO_LEASES, { HasSubleases: "Yes" }).ShowSubleaseDisclosure, null, "other answers missing");
    });

    T.test("without leases: No, blank and invalid -> No", function () {
        T.equal(calculate(NO_LEASES, answers("IFRS", "Large", "No")).ShowSubleaseDisclosure, "No", "No");
        T.equal(calculate(NO_LEASES, answers("IFRS", "Large", "")).ShowSubleaseDisclosure, "No", "empty string");
        T.equal(calculate(NO_LEASES, answers("IFRS", "Large", null)).ShowSubleaseDisclosure, "No", "null");
        T.equal(calculate(NO_LEASES, { Framework: "IFRS", EntitySize: "Large" }).ShowSubleaseDisclosure, "No", "missing");
        T.equal(calculate(NO_LEASES, answers("IFRS", "Large", "yes")).ShowSubleaseDisclosure, "No", "invalid");
    });

    // The 8 combinations HasSubleases Yes / No / blank / invalid x without / with leases (CLAUDE.md section 6, F-09).
    var F09_MATRIX = [
        { leases: false, hasSubleases: "Yes", expected: null },
        { leases: false, hasSubleases: "No", expected: "No" },
        { leases: false, hasSubleases: "", expected: "No" },
        { leases: false, hasSubleases: "maybe", expected: "No" },
        { leases: true, hasSubleases: "Yes", expected: "Yes" },
        { leases: true, hasSubleases: "No", expected: "No" },
        { leases: true, hasSubleases: "", expected: null },
        { leases: true, hasSubleases: "maybe", expected: null }
    ];

    function f09Label(row) {
        return (row.leases ? "with leases" : "without leases") + ", HasSubleases \"" + row.hasSubleases + "\" -> "
            + (row.expected === null ? "null" : row.expected);
    }

    for (var f09 = 0; f09 < F09_MATRIX.length; f09++) {
        (function (row) {
            T.test("F-09 matrix: " + f09Label(row), function () {
                var r = calculate(row.leases ? ONE_LEASE : NO_LEASES, answers("IFRS", "Large", row.hasSubleases));
                T.equal(r.ShowSubleaseDisclosure, row.expected, "ShowSubleaseDisclosure");
                T.hasExactKeys(r, RESULT_KEYS);
                if (!row.leases) {
                    // F-07 / F-08 stay No without leases, whatever HasSubleases is.
                    T.equal(r.ShowRouDisclosure, "No", "ShowRouDisclosure");
                    T.equal(r.ShowMaturityAnalysis, "No", "ShowMaturityAnalysis");
                } else {
                    T.equal(r.ShowRouDisclosure, "Yes", "ShowRouDisclosure");
                    T.equal(r.ShowMaturityAnalysis, "Yes", "ShowMaturityAnalysis");
                }
            });
        })(F09_MATRIX[f09]);
    }

    T.test("without leases, HasSubleases Yes: F-07 / F-08 stay No for every Framework / EntitySize", function () {
        var frameworks = ["IFRS", "LocalGAAP", "ifrs", null];
        var sizes = ["Micro", "Small", "Medium", "Large", "large", null];
        for (var f = 0; f < frameworks.length; f++) {
            for (var s = 0; s < sizes.length; s++) {
                var r = calculate(NO_LEASES, answers(frameworks[f], sizes[s], "Yes"));
                var label = frameworks[f] + " / " + sizes[s];
                T.equal(r.ShowRouDisclosure, "No", "F-07 " + label);
                T.equal(r.ShowMaturityAnalysis, "No", "F-08 " + label);
                T.equal(r.ShowSubleaseDisclosure, null, "F-09 " + label);
            }
        }
    });

    // -----------------------------------------------------------------------
    T.group("Invalid / missing answers (three-valued logic)");
    // -----------------------------------------------------------------------

    T.test("all answers missing with leases: counts and totals still calculated, Show fields null", function () {
        T.equalFields(calculate(ONE_LEASE, {}), expected({
            LeaseCount: 1, HasLeases: "Yes", TotalClosing: 1125000.45, AverageClosing: 1125000.45,
            ShowRouDisclosure: null, ShowMaturityAnalysis: null, ShowSubleaseDisclosure: null
        }));
    });

    T.test("all answers invalid (wrong case) with leases: Show fields null", function () {
        T.equalFields(calculate(ONE_LEASE, answers("ifrs", "large", "yes")), expected({
            LeaseCount: 1, HasLeases: "Yes", TotalClosing: 1125000.45, AverageClosing: 1125000.45,
            ShowRouDisclosure: null, ShowMaturityAnalysis: null, ShowSubleaseDisclosure: null
        }));
    });

    T.test("all answers invalid without leases: Show fields No", function () {
        T.equalFields(calculate(NO_LEASES, answers("ifrs", "large", "yes")), expected({}));
        T.equalFields(calculate(NO_LEASES, {}), expected({}));
    });

    // -----------------------------------------------------------------------
    T.group("Result shape and immutability");
    // -----------------------------------------------------------------------

    T.test("result has exactly the ten fields", function () {
        T.hasExactKeys(calculate(NO_LEASES, answers("IFRS", "Large", "Yes")), RESULT_KEYS, "0 leases");
        T.hasExactKeys(calculate(ONE_LEASE, answers("IFRS", "Large", "Yes")), RESULT_KEYS, "1 lease");
        T.hasExactKeys(calculate(SAMPLE_JSON, {}), RESULT_KEYS, "mixed, no answers");
        T.hasExactKeys(calculate(doc([lease("L-1", "EUR", "1.00", "0", "0", "0")]), answers("IFRS", "Large", "No")),
            RESULT_KEYS, "single EUR");
    });

    T.test("answers object is not mutated", function () {
        var a = answers("IFRS", "Large", "Yes");
        calculate(SAMPLE_JSON, a);
        T.equalFields(a, { Framework: "IFRS", EntitySize: "Large", HasSubleases: "Yes" });
        var empty = {};
        calculate(ONE_LEASE, empty);
        T.hasExactKeys(empty, []);
    });

    T.test("input object and its leases are not mutated by calculateFields", function () {
        var input = buildInput(SAMPLE_DKK_ONLY, answers("IFRS", "Large", "Yes"));
        calculateFields(input);
        T.hasExactKeys(input, INPUT_KEYS);
        T.equal(input.Framework, "IFRS");
        T.equal(input.EntitySize, "Large");
        T.equal(input.HasSubleases, "Yes");
        T.equal(input.Leases.length, 3);
        T.equal(input.Problems.length, 0);
        T.hasExactKeys(input.Leases[0], ["Line", "LeaseId", "AssetClass", "Description", "Currency",
            "Opening", "Additions", "Depreciation", "Disposals"]);
        T.equal(input.Leases[0].Opening, 1250000.5);
        T.equal(input.Leases[0].Depreciation, 125000.05);
        T.equal(input.Leases[1].Additions, 60000);
        T.equal(input.Leases[2].Currency, "DKK");
    });

    T.test("the same input gives the same result twice", function () {
        var input = buildInput(SAMPLE_DKK_ONLY, answers("IFRS", "Large", "Yes"));
        T.equalFields(calculateFields(input), calculateFields(input));
    });

    // -----------------------------------------------------------------------
    T.group("Answers isolation");
    // -----------------------------------------------------------------------

    T.test("extra answer properties are ignored and not copied into the input", function () {
        var a = { Framework: "IFRS", EntitySize: "Large", HasSubleases: "Yes",
            KpiRequired: "Yes", TotalCashOutflow: 1000, Foo: 1 };
        T.hasExactKeys(buildInput(ONE_LEASE, a), INPUT_KEYS);
        var r = calculate(ONE_LEASE, a);
        T.hasExactKeys(r, RESULT_KEYS);
        T.equal(r.SummaryRequired, null, "F-04");
        T.equal(r.ShowCashOutflow, null, "F-10");
        T.equalFields(r, calculate(ONE_LEASE, answers("IFRS", "Large", "Yes")), "same as without extras");
    });

    T.test("extra answer properties are ignored with no leases", function () {
        var r = calculate(NO_LEASES, { KpiRequired: "Yes", TotalCashOutflow: 1000, Foo: 1 });
        T.equalFields(r, expected({}));
    });

    // -----------------------------------------------------------------------
    T.group("Sample file (hand-written JSON)");
    // -----------------------------------------------------------------------

    T.test("sample: 4 leases, DKK + EUR -> MixedCurrency Yes, Total/Average null", function () {
        T.equalFields(calculate(SAMPLE_JSON, answers("IFRS", "Medium", "No")), expected({
            LeaseCount: 4, HasLeases: "Yes", MixedCurrency: "Yes", TotalClosing: null, AverageClosing: null,
            ShowRouDisclosure: "Yes", ShowMaturityAnalysis: "Yes", ShowSubleaseDisclosure: "No"
        }));
    });

    T.test("sample with LocalGAAP / Large / blank sublease answer", function () {
        T.equalFields(calculate(SAMPLE_JSON, answers("LocalGAAP", "Large", "")), expected({
            LeaseCount: 4, HasLeases: "Yes", MixedCurrency: "Yes", TotalClosing: null, AverageClosing: null,
            ShowRouDisclosure: "No", ShowMaturityAnalysis: "Yes", ShowSubleaseDisclosure: null
        }));
    });

    // -----------------------------------------------------------------------
    T.group("End-to-end with C# importer output (fixtures)");
    // -----------------------------------------------------------------------

    function fixture(name) {
        if (typeof Fixtures === "undefined" || typeof Fixtures[name] !== "string" || Fixtures[name].length === 0) {
            throw new Error("Fixture '" + name + "' was not loaded by the host.");
        }
        return Fixtures[name];
    }

    T.test("fixture leases.json (from samples/leases.csv)", function () {
        var json = fixture("leases");
        var input = buildInput(json, answers("IFRS", "Medium", "No"));
        T.equal(input.Leases.length, 4);
        T.equal(input.Problems.length, 0);
        T.equal(input.Leases[3].Currency, "EUR");
        T.equalFields(calculate(json, answers("IFRS", "Medium", "No")), expected({
            LeaseCount: 4, HasLeases: "Yes", MixedCurrency: "Yes", TotalClosing: null, AverageClosing: null,
            ShowRouDisclosure: "Yes", ShowMaturityAnalysis: "Yes", ShowSubleaseDisclosure: "No"
        }));
    });

    T.test("fixture leases_with_problems.json: only accepted leases count", function () {
        // Accepted: L-3001 1000000.00 + 50000.00 - 100000.00 = 950000.00
        //           L-3002 250000.25 - 50000.10 = 200000.15
        //           L-3005 30000.00 - 10000.01 = 19999.99
        // Total 1170000.14; average 117000014 / 3 = 39000004.67 cents -> 390000.05
        // Rejected (in Problems): duplicate L-3001, L-3003 "1.250,50", L-3004 XYZ, L-3006 EUR "abc".
        var json = fixture("leasesWithProblems");
        var input = buildInput(json, answers("LocalGAAP", "Large", "Yes"));
        T.equal(input.Leases.length, 3);
        T.equal(input.Leases[0].LeaseId, "L-3001");
        T.equal(input.Leases[1].LeaseId, "L-3002");
        T.equal(input.Leases[2].LeaseId, "L-3005");
        T.equal(input.Problems.length, 4);
        T.equal(input.Problems[0].Code, "DuplicateLeaseId");
        T.equal(input.Problems[1].Code, "InvalidNumber");
        T.equal(input.Problems[2].Code, "InvalidCurrency");
        T.equal(input.Problems[3].Code, "InvalidNumber");
        T.equalFields(calculate(json, answers("LocalGAAP", "Large", "Yes")), expected({
            LeaseCount: 3, HasLeases: "Yes", MixedCurrency: "No", TotalClosing: 1170000.14, AverageClosing: 390000.05,
            ShowRouDisclosure: "No", ShowMaturityAnalysis: "Yes", ShowSubleaseDisclosure: "Yes"
        }));
    });

    T.test("fixture leases_with_problems.json: Problems are ignored by the calculations", function () {
        // The same three accepted leases without any Problems give the same result.
        var withoutProblems = doc([
            lease("L-3001", "DKK", "1000000.00", "50000.00", "100000.00", "0"),
            lease("L-3002", "DKK", "250000.25", "0", "50000.10", "0"),
            lease("L-3005", "DKK", "30000.00", "0", "10000.01", "0.00")
        ]);
        var a = answers("IFRS", "Small", "No");
        T.equalFields(calculate(fixture("leasesWithProblems"), a), calculate(withoutProblems, a));
    });

    // -----------------------------------------------------------------------
    T.group("End-to-end with hand-written samples/expected JSON");
    // -----------------------------------------------------------------------

    T.test("samples/expected/leases-valid-dkk.json with IFRS / Medium / No", function () {
        // L-5001 2450000.00 + 150000.00 - 245000.00 - 50000.00 = 2305000.00
        // L-5002  612500.75 -  122500.15                      =  490000.60
        // L-5003  185000.40 -   37000.08                      =  148000.32
        // L-5004  920000.00 -   92000.00                      =  828000.00
        // L-5005   75000.01 -   15000.00                      =   60000.01
        // Total 3831000.93 = 383100093 cents; 383100093 / 5 = 76620018 remainder 3 -> 76620019 -> 766200.19
        var json = fixture("leasesValidDkk");
        var input = buildInput(json, answers("IFRS", "Medium", "No"));
        T.equal(input.Leases.length, 5);
        T.equal(input.Problems.length, 0);
        T.equal(input.Leases[0].Description, "Head office K\u00f8benhavn");
        T.equalFields(calculate(json, answers("IFRS", "Medium", "No")), expected({
            LeaseCount: 5, HasLeases: "Yes", MixedCurrency: "No", TotalClosing: 3831000.93, AverageClosing: 766200.19,
            ShowRouDisclosure: "Yes", ShowMaturityAnalysis: "Yes", ShowSubleaseDisclosure: "No"
        }));
    });

    T.test("samples/expected/leases-mixed.json with LocalGAAP / Large / Yes", function () {
        // Accepted (all DKK):
        // L-8001 (line 2)  800000.00 -  80000.00                       = 720000.00
        // L-8002 (line 3)  240000.50 + 30000.00 - 48000.10 - 10000.00  = 212000.40
        // L-8004 (line 8)   64000.33 -  12800.07                       =  51200.26
        // L-8005 (line 10) 150000.00 -  15000.00                       = 135000.00
        // Total 1118200.66 = 111820066 cents; 111820066 / 4 = 27955016 remainder 2 (exactly .5)
        //   -> half away from zero -> 27955017 -> 279550.17
        // Rejected rows (6 problems) are passed through and not counted.
        var json = fixture("leasesMixed");
        var input = buildInput(json, answers("LocalGAAP", "Large", "Yes"));
        T.equal(input.Leases.length, 4);
        T.equal(input.Problems.length, 6);
        T.equal(input.Leases[1].Description, "Pool cars, Aarhus\r\n6 vehicles");
        T.equal(input.Leases[3].Line, 10);
        T.equalFields(calculate(json, answers("LocalGAAP", "Large", "Yes")), expected({
            LeaseCount: 4, HasLeases: "Yes", MixedCurrency: "No", TotalClosing: 1118200.66, AverageClosing: 279550.17,
            ShowRouDisclosure: "No", ShowMaturityAnalysis: "Yes", ShowSubleaseDisclosure: "Yes"
        }));
    });

    T.test("samples/expected/leases-all-eur.json: single non-DKK currency is calculated (in EUR)", function () {
        // Agreed business risk (CLAUDE.md section 7, item 7): every lease is in EUR, so MixedCurrency is No and
        // the totals are calculated as the spec says, but they are EUR amounts, not the reporting currency DKK.
        // No currency field is added to the result.
        // L-9101 450000.00 + 25000.00 - 47500.00 - 0       = 427500.00 = 42750000 cents
        // L-9102  82000.50 +        0 - 16400.11 - 1200.00 =  64400.39 =  6440039 cents
        // L-9103  12500.00 +        0 -  2500.04 - 0       =   9999.96 =   999996 cents
        // Total 50190035 cents = 501900.35; 50190035 / 3 = 16730011 remainder 2 (0.67 >= 0.5)
        //   -> 16730012 cents = 167300.12
        var json = fixture("leasesAllEur");
        var input = buildInput(json, answers("IFRS", "Small", "No"));
        T.equal(input.Leases.length, 3);
        T.equal(input.Problems.length, 0);
        for (var i = 0; i < input.Leases.length; i++) {
            T.equal(input.Leases[i].Currency, "EUR", "lease " + i);
        }
        var r = calculate(json, answers("IFRS", "Small", "No"));
        T.hasExactKeys(r, RESULT_KEYS);
        T.equalFields(r, expected({
            LeaseCount: 3, HasLeases: "Yes", MixedCurrency: "No", TotalClosing: 501900.35, AverageClosing: 167300.12,
            ShowRouDisclosure: "Yes", ShowMaturityAnalysis: "Yes", ShowSubleaseDisclosure: "No"
        }));
    });

    T.test("samples/expected/leases-unusual-text.json: buildInput passes text through unchanged", function () {
        // 🏢 = U+1F3E2 office building, 🚗 = U+1F697 car,
        // 𝔸 = U+1D538 double-struck A, 𠜎 = U+2070E CJK ideograph, ́ = combining acute.
        var input = buildInput(fixture("leasesUnusualText"), answers("LocalGAAP", "Medium", "Yes"));
        var leases = input.Leases;
        T.equal(leases.length, 7);
        T.equal(leases[0].Description, "Office \"Nord\" Aarhus", "escaped quotes");
        T.equal(leases[1].Description, "Company car 🚗 Odense", "car emoji");
        T.equal(leases[2].Description, "Math 𝔸 and rare 𠜎 glyphs", "4-byte letters");
        T.equal(leases[3].Description, "Lager Ærøskøbing Øst Åbyhøj æøå ÆØÅ",
            "Danish letters");
        T.equal(leases[4].Description, "Vesterbro Café", "combining accent");
        T.equal(leases[4].Description.length, 15, "combining accent is a separate code unit, not normalized");
        T.equal(leases[5].Description, "🏢 HQ, \"Tower A\" 🏢", "comma, quotes and emoji");
        T.equal(leases[6].AssetClass, "🏢 Buildings", "AssetClass with emoji");
        T.equal(leases[1].Description.length, "Company car  Odense".length + 2, "emoji is a surrogate pair");
        T.equal(leases[6].AssetClass.charCodeAt(0), 0xD83C, "high surrogate");
        T.equal(leases[6].AssetClass.charCodeAt(1), 0xDFE2, "low surrogate");
        T.equal(input.Problems.length, 1);
        T.equal(input.Problems[0].Code, "InvalidCurrency");
        T.equal(input.Problems[0].Line, 9);
        T.equal(input.Problems[0].Message, "This lease can't be processed because 'D🏢K' is not a valid "
            + "ISO-4217 currency code (use three uppercase letters, e.g. DKK).", "4-byte character in the message");
    });

    T.test("samples/expected/leases-unusual-text.json: calculation with LocalGAAP / Medium / Yes", function () {
        // Accepted (all DKK); the D<emoji>K row is rejected and not counted.
        // L-7001 100000.00 - 10000.00 =  90000.00 =  9000000 cents
        // L-7002  50000.50 -  5000.05 =  45000.45 =  4500045 cents
        // L-7003   1000.01 -     0    =   1000.01 =   100001 cents
        // L-7004  20000.00 -  2000.00 =  18000.00 =  1800000 cents
        // L-7005   3000.00 -  1000.00 =   2000.00 =   200000 cents
        // L-7006 500000.00 - 50000.00 = 450000.00 = 45000000 cents
        // L-7007   7000.00 -     0.01 =   6999.99 =   699999 cents
        // Total 61300045 cents = 613000.45; 61300045 / 7 = 8757149 remainder 2 (0.29 < 0.5) -> 87571.49
        T.equalFields(calculate(fixture("leasesUnusualText"), answers("LocalGAAP", "Medium", "Yes")), expected({
            LeaseCount: 7, HasLeases: "Yes", MixedCurrency: "No", TotalClosing: 613000.45, AverageClosing: 87571.49,
            ShowRouDisclosure: "No", ShowMaturityAnalysis: "No", ShowSubleaseDisclosure: "Yes"
        }));
    });
})();
