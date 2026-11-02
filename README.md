
Take-Home Exercise: Lease Import

A C# console application reads a lease CSV file, validates every value and produces a JSON document. A classic JScript (ECMAScript 3) script receives that JSON together with the engagement answers and calculates the disclosure fields F-01 to F-10 defined in the spec.

This README explains how I approached the exercise, step by step, which decisions I made and why, and how I used AI tools along the way. It is also my notes file for step D of the brief: the problems I found in the spec and how I handled each one are in section 6.

How to run it
From the solution folder:

dotnet run --project LeaseImport -- samples/leases.csv samples/leases.json
dotnet test LeaseImportAssignment.slnx
cscript //nologo jscript/tests/run-cscript.wsf
node jscript/tests/run-node.js

The first command imports the sample file. If you leave out the output path, the JSON is printed to the console. The importer returns exit code 0 when it has written the JSON, even if some rows were rejected, and 1 if the arguments are wrong or the file can't be read.

The C# tests run with dotnet test or from Test Explorer in Visual Studio. The JScript tests run on the real classic JScript engine with cscript (Windows), and also under Node, which I used during development.

1. I started with the JSON
After reading the brief, the first thing I did was decide what the JSON between the two parts should look like. It's the contract between C# and JScript, so everything else depends on it.

The brief only says it must contain "the accepted leases and the problems found", so I kept it to exactly that: two lists.

{
  "Leases": [
    {
      "Line": 2,
      "LeaseId": "L-2001",
      "AssetClass": "Property",
      "Description": "Office Aarhus",
      "Currency": "DKK",
      "Opening": 1250000.50,
      "Additions": 0,
      "Depreciation": 125000.05,
      "Disposals": 0
    }
  ],
  "Problems": [
    {
      "Line": 5,
      "LeaseId": "L-2001",
      "Field": "LeaseId",
      "Code": "DuplicateLeaseId",
      "Message": "This lease can't be processed because its ID 'L-2001' is not unique (first used on line 2)."
    }
  ]
}
A few things I decided on purpose:

The lease fields have the same names as the CSV columns, so it's easy to see where each value comes from.
Every lease and every problem has its line number. The brief makes a point of physical line numbers, and without them a user can't find the bad row in the file.
A problem has a code for the program and the tests, and a message for a person. The messages are written for a business user and include the useful details: the value, the field and the line. They all live in one class, so the wording stays consistent.
Amounts are numbers, not text. In JScript, text would be glued together instead of added ("100" + "50" gives "10050").
I left out everything that wasn't needed: no severity levels (any failed check rejects the row), and no totals or closing values. All calculations happen in JScript, so the formula exists in one place only.

2. Then I planned the project structure
I wanted the simplest structure that still keeps different responsibilities apart, so each piece can be tested on its own.

LeaseImport/                C# console app (.NET 10)
  Common/
    Models.cs               Lease, Problem, ImportResult
    ProblemMessages.cs      the message for each problem code
    Constants.cs            constants used by more than one class
  CsvParser.cs              reads the CSV text into rows
  LeaseValidator.cs         checks the values of one row
  LeaseImporter.cs          puts it together: header, rows, duplicates, JSON
  Program.cs                command line: arguments, files, exit code
LeaseImport.Tests/          xUnit tests
jscript/LeaseCalculator.js  the ES3 calculator
jscript/tests/              the JScript tests
samples/                    the sample CSV, extra test files and their expected JSON
Why it looks like this:

Program.cs only deals with the command line. All the logic is in other classes, so if this had to run inside a desktop app or behind an API later, only that thin layer would change.
The parser, the validator and the importer are separate. The parser only knows CSV syntax (quotes, line breaks, the BOM). The validator only knows what a valid lease looks like. The importer connects them and handles rules that involve more than one row, like duplicate IDs.
Common/ is for shared things, not logic: the models, the messages and a few constants. I only moved a constant there if more than one class uses it.
The models are plain public classes. At first they were internal sealed record types. I changed them because this is a small console app and plain classes are easier for anyone to read. For the same reason I dropped internal: nothing uses a console app except its own tests, so it didn't protect anything. In a shared library I would keep things internal.
The parser reads the file as a stream, one row at a time, instead of loading the whole file into memory first. Problems come out in line order, and the JSON is written straight to the output file. The two lists in the JSON are still built in memory, because one comes after the other in the file. Streaming those too would mean changing the format, and nobody asked for that.

3. I split the AI work into agents
I used Claude Code for this exercise. Instead of one long conversation where the AI does everything, I set up five agents, each with one job:

analyze-brief reviews the requirements from a business point of view and compares our decisions with the original brief. It can't change files.
json-importer writes the C# import and validation code.
jscript-generator writes the JScript calculator.
unit-tests-generator writes and runs the tests.
code-reviewer reviews the code against the rules. It can run tests but can't change files.
All of them read the same file, CLAUDE.md, which contains every decision I agreed to. If an agent's work disagrees with that file, the file wins.

The reason for the split: I didn't want the same agent to write the code and the tests, because tests written by looking at the code tend to repeat its mistakes. And I wanted the review and analysis to come from agents that can only report, so the decisions stay with me.

4. I looked for problems hidden in the CSV file
Using the analysis agent's review and my own look at the sample file, I found a few things the brief doesn't mention but that would break a real import.

The BOM. The sample file starts with three invisible bytes that mark it as UTF-8. If you don't remove them, the first column is read as "﻿LeaseId" instead of "LeaseId". It looks the same on screen, but the header check fails and reports a column as missing when you can clearly see it. The parser removes the BOM, and there are tests for files with and without it. I was also careful not to open the sample in Excel, because saving it there would change it.

Danish number formats. The reporting currency is DKK, so the clients are most likely Danish. On a Danish computer the dot separates thousands and the comma separates decimals. If the code parsed numbers using the computer's settings, 1250000.50 could quietly become 125 million. So amounts are always parsed in one fixed format, whatever the computer's settings. A test runs the whole import with Danish settings to prove the result doesn't change. I also added two sample files that show what a Danish user might really send: amounts written like 1.250.000,50, and a file saved by Danish Excel with semicolons instead of commas. Both are rejected with a clear problem instead of being guessed.

A strict number format. An amount must use a dot for decimals, with no thousands separators, no exponent (1e5), at most two decimals, and it can't be negative. I chose to reject anything else rather than try to guess what the user meant, because guessing separators is exactly how financial figures go wrong without anyone noticing. A good example is 1.250: in the fixed format it is a valid number with three decimals, so it is rejected for having too many decimals, while a Danish user would have meant 1,250. Rejecting it with a clear message is better than accepting the wrong value. TODO(verify): final code and message for "1.250"

Strict UTF-8. If the file contains bytes that aren't valid UTF-8, the import reports it instead of silently replacing them with �, which would quietly damage descriptions. TODO(verify): how an invalid-UTF-8 file is reported

Currencies. Currency codes are checked against a list of active ISO-4217 codes. Lowercase codes like dkk are rejected, not silently fixed. Currencies other than DKK are valid, because handling mixed currencies is the JScript part's job. One open point: Bulgaria adopted the euro on 1 January 2026, so BGN is no longer an active code, but a lease file for an earlier period could still contain it. A grace period (for example six months after a currency is withdrawn) could handle that, but how long it should be, and whether it should depend on today's date or on the reporting period, is a business decision. I left it as a question rather than invent a rule. TODO(verify): final decision on BGN

Other things I handled:

Windows line endings (\r\n) as well as Unix ones.
Descriptions with commas, quotes or even line breaks inside quotes. That's also why problems report the line where the row starts, which is what "physical line" means in the brief.
An empty amount is reported as missing, not treated as 0, because 0 is a real value.
When the same lease ID appears twice, the first row is kept and the later ones are rejected.
If a required column is missing or appears twice, the whole file is rejected, because no row can be read correctly.

5. I tested good cases, bad cases and edge cases
For each rule I wanted three kinds of tests:

Good input, like the official sample (4 leases, no problems) or a file with five valid DKK leases.
Bad input, like invalid numbers, too many decimals, negative amounts, unknown currencies, missing columns or duplicate IDs. A row with several mistakes must report all of them, not just the first one.
Edge cases, like an empty file, no leases at all, mixed currencies, rounding values that end in 5, small floating-point traps like 0.1 + 0.2, and very large amounts.
For the JScript part I focused on the business rules. One test goes through every combination of the answers (framework, entity size, subleases, with and without leases) and checks which disclosures should be shown. The expected results are written as a table, so you can check them without reading the code.

I also added sample CSV files with their expected JSON written by hand, and a test that imports each one and compares the result. I insisted on writing the expected JSON by hand: if the importer generated it, the test would only be comparing the program with itself.

To make sure the tests actually catch mistakes, I broke a rule on purpose (I removed the brackets in F-08) and checked that a test failed. Then I put the code back.

6. Problems I found in the spec, and what I did about each one
The brief asks to review the spec as you would before implementing it. These are the problems I found, mostly in the "Fields to calculate" table. One sentence in the brief guided most of my decisions: "Anything the spec refers to that is not in this list does not exist."

F-04 SummaryRequired uses KpiRequired, which doesn't exist. It isn't an input and it isn't calculated anywhere. I return null, meaning "can't be calculated". I could have returned "No", but that would hide a broken definition behind an answer that looks correct.

F-10 ShowCashOutflow uses TotalCashOutflow, which doesn't exist either. Same reasoning, so it returns null. There's no payment data in the CSV, so I didn't invent a formula for it.

F-08 ShowMaturityAnalysis has no brackets. It says HasLeases = Yes AND Framework = IFRS OR EntitySize = Large. Read with normal precedence, a large company with no leases at all would show a maturity analysis of leases it doesn't have. I implemented it as HasLeases = Yes AND (Framework = IFRS OR EntitySize = Large), which matches F-07 and makes sense in accounting terms. A test shows the difference between the two readings.

F-09 ShowSubleaseDisclosure contradicts itself. It says HasSubleases = Yes AND HasLeases = No. But the subleases question is skipped when there are no leases, and you can only sublease something you lease yourself, so as written it could never be "Yes". I implemented it as HasSubleases = Yes AND HasLeases = Yes. If the answers say there are subleases but there are no accepted leases, the result is "No". That combination means either the question shouldn't have been answered or some leases are missing (maybe rejected by validation). I didn't add an extra output field for it, because the result is defined as exactly the ten fields, but the business might want to block that combination earlier, in the questionnaire.

F-06 AverageClosing can divide by zero. With no leases there's nothing to divide by, and when the total is null there's nothing to average. Both give null. The average is calculated from the exact total and rounded only once, at the end.

F-05 TotalClosing with no leases. The sum of nothing is 0, so the total is 0 while the average stays null.

F-05 with a single currency that isn't DKK. If every lease is in EUR, the currencies aren't mixed, so the spec says to calculate the total. But it's a total in EUR, while the reporting currency is DKK, and nothing in the result says so. There are no exchange rates in the inputs, so I calculate it as the spec says and flag it here as a business risk, rather than invent a conversion or an extra field.

The sample file gives null totals. Lease L-2004 is in EUR, so the sample has mixed currencies, and TotalClosing and AverageClosing are null. That's correct, not a bug. EUR is a valid currency code, so the C# part accepts the row. If I rejected it there, mixed currency (F-03) could never be "Yes".

Totals only cover accepted leases. Rows rejected by the importer don't reach the calculator, so when there are problems, the totals are partial. The result itself doesn't say that, but the problems are passed along with the leases, so the application can show them next to the numbers.

Invalid or missing answers aren't covered. The spec doesn't say what happens with "ifrs" in lowercase, a missing entity size, or a blank subleases answer when there are leases. Answers must match exactly. If a disclosure depends on an answer that is missing or invalid, the result is null, unless the outcome is already decided: for example, with no leases the Show fields are "No" whatever the other answers are.

Rounding. The spec asks for rounding half away from zero. JScript's Math.round doesn't do that for negative numbers (-2.5 becomes -2), and floating-point numbers can't store values like 1.005 exactly. So the calculator works in whole cents: each amount is converted once, the sums are done on whole numbers, and the average is rounded with integer arithmetic. Whole numbers are exact in JScript up to 2^53, which is about 90 trillion in amount, far more than any real lease portfolio, so I only note the limit instead of adding a rule for it.

Section 5 is missing. The brief's numbering goes from 4 to 6. Nothing to implement, but worth mentioning in case something was left out.

What I chose not to build
No HTTP API. The brief asks for a C# program that reads a CSV file, and says that how the JSON reaches the script is out of scope. An API would have meant more code and more places for bugs, for something nobody asked for. Because the logic is separate from Program.cs, adding one later would be easy.

No FluentValidation or other packages. FluentValidation is great for validating objects, but here most of the work is turning text into numbers, and that already is the validation: decimal.TryParse tells you whether the value is valid and gives you the number in the same step. With FluentValidation I'd need an extra class with every field as text, I'd parse each amount twice, and I'd have to map its errors back to my own codes. For eight columns that's not worth it. I also wrote the CSV parser myself, because I needed full control over line numbers, and I used the built-in JSON serializer.

No async code. The program reads one local file once and has nothing else to do while it waits. Async would add complexity without making anything faster. If this ran inside a desktop app, I would keep this code as it is and call it in the background from the UI, so the window doesn't freeze.

How I used AI
How I worked
I didn't let the AI start by writing code. I first did my own analysis of the brief, then asked the AI to review the spec and list the problems it found, with a clear instruction not to create any files yet. I compared its list with mine and decided on each point myself. Only after that did I write the decisions into CLAUDE.md and let the agents start, one phase at a time: first the C# import, then the JScript calculator, then the extra test files. After each phase I asked for the real test output and a code review.

I then checked the results myself: I ran the C# tests in Visual Studio, ran the JScript tests with cscript on Windows, and worked out some key values by hand.

Where it helped
It was fast at reviewing the spec against my own list and suggesting edge cases I could then accept or reject. It also wrote the CSV parser, the validation rules, the repetitive parts of the tests and the sample files, which I then checked. Once I had listed the ES3 restrictions clearly, it wrote code that respected them.

Where I corrected it
The thing I noticed most is that the AI likes to add things nobody asked for, and it can sound very sure about something that is wrong. Some examples:

It suggested adding a ClosingCurrency field to the result. I said no: the spec defines the result fields, and the currency risk belongs in the notes, not in an invented output.
It suggested checking for a negative closing value in C#. I said no, because the closing value is calculated in JScript, and doing it in C# as well would put the same formula in two places.
It suggested severity levels for problems, accepting the answers as a JSON string as well as an object, and an extra output field for the F-09 inconsistency. I rejected all of these to keep the solution to what the brief asks for.
It proposed a volume test with 10,000 leases of 9,999,999,999.99 and said it stayed within the 2^53 limit. It doesn't: that's about 10^16 cents, above the limit of about 9·10^15. The test wouldn't have proved what it claimed, so I had the numbers changed.
It proposed a number check that let -0 through. Numerically, -0 isn't below zero, so a check on the parsed value accepted it, even though the text is negative. TODO(verify): how it was fixed
Its expected result for 1.250 didn't match the rules we had agreed on. TODO(verify): describe the exact mismatch
It planned a Danish test file with 1.250.000,50 unquoted in a comma-separated file. The commas would have split the value into extra columns, so the test would have checked the column count instead of the number format. Danish Excel actually saves files with semicolons, so I asked for two files: one with quoted Danish amounts and one with semicolons.
Where I wouldn't trust it in this kind of work
Deciding what an ambiguous business rule means. It can list the possible readings, but choosing one is a business decision, and I made each one myself.
Money calculations. The usual mistakes are double instead of decimal, Math.Round with banker's rounding, toFixed in JScript, and forgetting negative zero.
Old platforms. By default it writes modern JavaScript (forEach, JSON.parse, const, trailing commas), which breaks in classic JScript. Only running the tests on the real engine proves the code works.
Numbers it works out itself. I checked the key values by hand, for example a total of 1,584,000.45 and an average of 528,000.15 for the three DKK leases in the sample.
Expected values in tests. If the expected values come from the code being tested, the test proves nothing. That's why the expected values and the expected JSON files are written by hand.
"All tests pass." I only trusted real test output, and I ran the tests myself.
The full AI session history is included with the hand-in, as the brief asks.

