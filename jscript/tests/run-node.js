// run-node.js - Node host for the ES3 tests (this file only may use modern JavaScript).
// Supplies the JsonParser host object, the hand-written samples/expected JSON, loads the ES3 files into one
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
        leases: readUtf8(path.join(expectedDir, "leases.json")),
        leasesWithProblems: readUtf8(path.join(expectedDir, "leases_with_problems.json")),
        leasesValidDkk: readUtf8(path.join(expectedDir, "leases-valid-dkk.json")),
        leasesMixed: readUtf8(path.join(expectedDir, "leases-mixed.json")),
        leasesAllEur: readUtf8(path.join(expectedDir, "leases-all-eur.json")),
        leasesUnusualText: readUtf8(path.join(expectedDir, "leases-unusual-text.json")),
    },
});

const es3Files = [
    path.join(testsDir, "TestRunner.js"),
    path.join(jscriptDir, "LeaseCalculator.js"),
    path.join(testsDir, "LeaseCalculatorTests.js"),
];

// cscript reads files included with <script src="..."> in the ANSI code page, so any non-ASCII
// character in these files would be misread on Windows. Non-ASCII text must use \u escapes.
const asciiOffenders = [];
for (const file of es3Files.concat([path.join(testsDir, "run-cscript.wsf")])) {
    readUtf8(file).split(/\r?\n/).forEach((line, index) => {
        if (/[^\x00-\x7F]/.test(line)) {
            asciiOffenders.push(`${path.relative(jscriptDir, file)}:${index + 1}`);
        }
    });
}
if (asciiOffenders.length > 0) {
    console.log("FAIL [ASCII only] non-ASCII characters found (use \\u escapes): " + asciiOffenders.join(", "));
    process.exitCode = 1;
    return;
}

for (const file of es3Files) {
    vm.runInContext(readUtf8(file), context, { filename: file });
}

context.writeLine = (text) => console.log(text);
const failed = vm.runInContext("TestRunner.run(writeLine)", context);
process.exitCode = failed === 0 ? 0 : 1;
