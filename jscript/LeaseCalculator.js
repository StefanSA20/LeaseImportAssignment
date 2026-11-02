// LeaseCalculator.js - strict ECMAScript 3 (classic JScript).
// Combines the C# importer JSON with the engagement answers and calculates F-01..F-10.
// The host must provide JsonParser.parse(text).

var YES = "Yes";
var NO = "No";

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

function buildInput(leasesJson, answers) {
    if (typeof leasesJson !== "string") {
        throw new Error("buildInput: leasesJson must be a string.");
    }
    if (answers === null || typeof answers !== "object" || isArray(answers)) {
        throw new Error("buildInput: answers must be a plain object.");
    }

    var parsed = JsonParser.parse(leasesJson);
    if (parsed === null || typeof parsed !== "object" || isArray(parsed)) {
        throw new Error("buildInput: leasesJson must contain a JSON object.");
    }

    return {
        Framework: readAnswer(answers, "Framework"),
        EntitySize: readAnswer(answers, "EntitySize"),
        HasSubleases: readAnswer(answers, "HasSubleases"),
        Leases: copyArray(parsed.Leases, "Leases"),
        Problems: copyArray(parsed.Problems, "Problems")
    };
}

function calculateFields(input) {
    var leases = input.Leases;
    var leaseCount = leases.length;
    var hasLeases = leaseCount > 0 ? YES : NO;
    var mixedCurrency = hasMixedCurrency(leases) ? YES : NO;

    var totalCents = null;
    if (mixedCurrency === NO) {
        totalCents = sumClosingCents(leases);
    }

    var averageCents = null;
    if (leaseCount > 0 && totalCents !== null) {
        averageCents = divideRoundHalfAwayFromZero(totalCents, leaseCount);
    }

    var isIfrs = answerEquals(input.Framework, "IFRS", FRAMEWORK_VALUES);
    var isLarge = answerEquals(input.EntitySize, "Large", ENTITY_SIZE_VALUES);
    var hasSubleases = subleaseAnswer(input.HasSubleases);

    return {
        LeaseCount: leaseCount,
        HasLeases: hasLeases,
        MixedCurrency: mixedCurrency,
        SummaryRequired: null,
        TotalClosing: centsToAmount(totalCents),
        AverageClosing: centsToAmount(averageCents),
        ShowRouDisclosure: and3(hasLeases, isIfrs),
        ShowMaturityAnalysis: and3(hasLeases, or3(isIfrs, isLarge)),
        ShowSubleaseDisclosure: and3(hasSubleases, hasLeases),
        ShowCashOutflow: null
    };
}

function calculate(leasesJson, answers) {
    return calculateFields(buildInput(leasesJson, answers));
}

// ---------------------------------------------------------------------------
// Answers
// ---------------------------------------------------------------------------

var FRAMEWORK_VALUES = ["IFRS", "LocalGAAP"];
var ENTITY_SIZE_VALUES = ["Micro", "Small", "Medium", "Large"];

function readAnswer(answers, name) {
    if (!Object.prototype.hasOwnProperty.call(answers, name) || answers[name] === undefined) {
        return null;
    }
    return answers[name];
}

// "Yes" / "No" when the answer is one of the allowed values, null when it is invalid or missing.
function answerEquals(answer, wanted, allowedValues) {
    if (!containsString(allowedValues, answer)) {
        return null;
    }
    return answer === wanted ? YES : NO;
}

// HasSubleases: "Yes"/"No" pass through; blank ("", null, missing) and invalid values are unknown.
function subleaseAnswer(answer) {
    if (answer === YES || answer === NO) {
        return answer;
    }
    return null;
}

// Three-valued AND: "No" wins, then unknown (null), then "Yes".
function and3(a, b) {
    if (a === NO || b === NO) {
        return NO;
    }
    if (a !== YES || b !== YES) {
        return null;
    }
    return YES;
}

// Three-valued OR: "Yes" wins, then unknown (null), then "No".
function or3(a, b) {
    if (a === YES || b === YES) {
        return YES;
    }
    if (a !== NO || b !== NO) {
        return null;
    }
    return NO;
}

// ---------------------------------------------------------------------------
// Leases and money (integer cents)
// ---------------------------------------------------------------------------

function hasMixedCurrency(leases) {
    if (leases.length === 0) {
        return false;
    }
    var first = leases[0].Currency;
    for (var i = 1; i < leases.length; i++) {
        if (leases[i].Currency !== first) {
            return true;
        }
    }
    return false;
}

function sumClosingCents(leases) {
    var total = 0;
    for (var i = 0; i < leases.length; i++) {
        total += closingCents(leases[i]);
    }
    return total;
}

// Opening + Additions - Depreciation - Disposals, in integer cents.
function closingCents(lease) {
    return toCents(lease.Opening, "Opening")
        + toCents(lease.Additions, "Additions")
        - toCents(lease.Depreciation, "Depreciation")
        - toCents(lease.Disposals, "Disposals");
}

// Amounts have at most 2 decimals, so amount * 100 is within float noise of an integer
// (e.g. 1.15 * 100 = 114.99999999999999). Take the nearest integer, rounding the magnitude.
function toCents(amount, fieldName) {
    if (typeof amount !== "number" || !isFinite(amount)) {
        throw new Error("Lease amount '" + fieldName + "' is not a finite number.");
    }
    var x = amount * 100;
    var cents = x >= 0 ? Math.floor(x + 0.5) : -Math.floor(-x + 0.5);
    return cents + 0; // turns -0 into 0
}

// Integer division of cents by a positive count, rounded half away from zero.
function divideRoundHalfAwayFromZero(cents, count) {
    var magnitude = cents < 0 ? -cents : cents;
    var quotient = Math.floor(magnitude / count);
    var remainder = magnitude - quotient * count;
    if (2 * remainder >= count) {
        quotient++;
    }
    return cents < 0 ? -quotient + 0 : quotient;
}

function centsToAmount(cents) {
    if (cents === null) {
        return null;
    }
    return cents / 100;
}

// ---------------------------------------------------------------------------
// ES3 helpers
// ---------------------------------------------------------------------------

function isArray(value) {
    return Object.prototype.toString.call(value) === "[object Array]";
}

function copyArray(value, name) {
    if (!isArray(value)) {
        throw new Error("buildInput: '" + name + "' must be a JSON array.");
    }
    var copy = [];
    for (var i = 0; i < value.length; i++) {
        copy[copy.length] = value[i];
    }
    return copy;
}

function containsString(values, candidate) {
    if (typeof candidate !== "string") {
        return false;
    }
    for (var i = 0; i < values.length; i++) {
        if (values[i] === candidate) {
            return true;
        }
    }
    return false;
}
