// TestRunner.js - tiny assertion framework in strict ECMAScript 3 (classic JScript).
// Usage: TestRunner.group("name"); TestRunner.test("name", function () { ... });
//        var failed = TestRunner.run(writeLine);  // writeLine(text) is supplied by the host

var TestRunner = (function () {
    var tests = [];
    var currentGroup = "";

    function AssertionError(message) {
        this.message = message;
    }

    function fail(message) {
        throw new AssertionError(message);
    }

    function describe(value) {
        if (value === null) {
            return "null";
        }
        if (value === undefined) {
            return "undefined";
        }
        if (typeof value === "string") {
            return "\"" + value + "\"";
        }
        if (typeof value === "number" && value === 0 && 1 / value < 0) {
            return "-0";
        }
        if (Object.prototype.toString.call(value) === "[object Array]") {
            var parts = [];
            for (var i = 0; i < value.length; i++) {
                parts[parts.length] = describe(value[i]);
            }
            return "[" + parts.join(", ") + "]";
        }
        return String(value) + " (" + typeof value + ")";
    }

    function prefix(message) {
        return message ? message + ": " : "";
    }

    // Strict equality; also tells 0 and -0 apart so that amounts never come back as -0.
    function equal(actual, expected, message) {
        var same = actual === expected;
        if (same && actual === 0 && 1 / actual !== 1 / expected) {
            same = false;
        }
        if (!same) {
            fail(prefix(message) + "expected " + describe(expected) + " but got " + describe(actual));
        }
    }

    function isTrue(condition, message) {
        if (condition !== true) {
            fail(prefix(message) + "expected true");
        }
    }

    function ownKeys(object) {
        var keys = [];
        for (var key in object) {
            if (Object.prototype.hasOwnProperty.call(object, key)) {
                keys[keys.length] = key;
            }
        }
        keys.sort();
        return keys;
    }

    // The object has exactly these own property names (order does not matter).
    function hasExactKeys(object, expectedKeys, message) {
        var actual = ownKeys(object);
        var expected = [];
        for (var i = 0; i < expectedKeys.length; i++) {
            expected[expected.length] = expectedKeys[i];
        }
        expected.sort();
        if (actual.join(",") !== expected.join(",")) {
            fail(prefix(message) + "expected keys " + describe(expected) + " but got " + describe(actual));
        }
    }

    // Every property of `expected` equals the same property of `actual`, and there are no others.
    function equalFields(actual, expected, message) {
        hasExactKeys(actual, ownKeys(expected), message);
        for (var key in expected) {
            if (Object.prototype.hasOwnProperty.call(expected, key)) {
                equal(actual[key], expected[key], prefix(message) + key);
            }
        }
    }

    // fn must throw. Returns the thrown value so callers can inspect it.
    function assertThrows(fn, message) {
        var threw = false;
        var error = null;
        try {
            fn();
        } catch (e) {
            if (e instanceof AssertionError) {
                throw e;
            }
            threw = true;
            error = e;
        }
        if (!threw) {
            fail(prefix(message) + "expected an exception, but none was thrown");
        }
        return error;
    }

    // fn must throw an instance of Error (from this script context).
    function assertThrowsError(fn, message) {
        var error = assertThrows(fn, message);
        if (!(error instanceof Error)) {
            fail(prefix(message) + "expected an Error object but got " + describe(error));
        }
        return error;
    }

    function assertNoThrow(fn, message) {
        try {
            return fn();
        } catch (e) {
            if (e instanceof AssertionError) {
                throw e;
            }
            fail(prefix(message) + "unexpected exception: " + (e && e.message ? e.message : describe(e)));
        }
        return undefined;
    }

    function group(name) {
        currentGroup = name;
    }

    function test(name, fn) {
        tests[tests.length] = { group: currentGroup, name: name, fn: fn };
    }

    function errorText(e) {
        if (e instanceof AssertionError) {
            return e.message;
        }
        if (e && e.message) {
            return "unexpected exception: " + e.message;
        }
        return "unexpected exception: " + describe(e);
    }

    // Runs every registered test and returns the number of failures.
    function run(writeLine) {
        var passed = 0;
        var failed = 0;
        var groupCounts = {};
        var groupOrder = [];
        for (var i = 0; i < tests.length; i++) {
            var t = tests[i];
            if (!Object.prototype.hasOwnProperty.call(groupCounts, t.group)) {
                groupCounts[t.group] = 0;
                groupOrder[groupOrder.length] = t.group;
            }
            groupCounts[t.group]++;
            try {
                t.fn();
                passed++;
            } catch (e) {
                failed++;
                writeLine("FAIL [" + t.group + "] " + t.name + " - " + errorText(e));
            }
        }
        for (var g = 0; g < groupOrder.length; g++) {
            writeLine("  " + groupOrder[g] + ": " + groupCounts[groupOrder[g]] + " tests");
        }
        writeLine((failed === 0 ? "OK" : "FAILED") + " - " + passed + " passed, " + failed + " failed, "
            + tests.length + " total");
        return failed;
    }

    return {
        group: group,
        test: test,
        run: run,
        equal: equal,
        isTrue: isTrue,
        hasExactKeys: hasExactKeys,
        equalFields: equalFields,
        assertThrows: assertThrows,
        assertThrowsError: assertThrowsError,
        assertNoThrow: assertNoThrow
    };
})();
