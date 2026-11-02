// run-node.js - Node host for the ES3 tests (this file only may use modern JavaScript).
// Supplies the JsonParser host object, the C# fixtures and the hand-written samples/expected JSON, loads the ES3 files into one
// vm context and exits non-zero when a test fails.
"use strict";

const fs = require("fs");
const path = require("path");
const vm = require("vm");

const testsDir = __dirname;
const jscriptDir = path.join(testsDir, "..");
const expectedDir = path.join(jscriptDir, "..", "samples", "expected");

function readUtf8(file) {
    return fs.readFileSync(file, "utf8").replace(/^﻿/, "");
}

const context = vm.createContext({
    JsonParser: { parse: (text) => JSON.parse(text) },
    Fixtures: {
        leases: readUtf8(path.join(testsDir, "fixtures", "leases.json")),
        leasesWithProblems: readUtf8(path.join(testsDir, "fixtures", "leases_with_problems.json")),
        leasesValidDkk: readUtf8(path.join(expectedDir, "leases-valid-dkk.json")),
        leasesMixed: readUtf8(path.join(expectedDir, "leases-mixed.json")),
        leasesAllEur: readUtf8(path.join(expectedDir, "leases-all-eur.json")),
        leasesUnusualText: readUtf8(path.join(expectedDir, "leases-unusual-text.json")),
    },
});

for (const file of [
    path.join(testsDir, "TestRunner.js"),
    path.join(jscriptDir, "LeaseCalculator.js"),
    path.join(testsDir, "LeaseCalculatorTests.js"),
]) {
    vm.runInContext(readUtf8(file), context, { filename: file });
}

context.writeLine = (text) => console.log(text);
const failed = vm.runInContext("TestRunner.run(writeLine)", context);
process.exitCode = failed === 0 ? 0 : 1;
