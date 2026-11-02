using LeaseImport.Common;
using Xunit.Sdk;
using static LeaseImport.Tests.Tests.ModelAssert;

namespace LeaseImport.Tests.Tests;

/// <summary>The model classes have no value equality, so the comparison helpers must notice every property.</summary>
public class ModelAssertTests
{
    private static Lease NewLease() => new()
    {
        Line = 2, LeaseId = "L-1", AssetClass = "Property", Description = "Office", Currency = "DKK",
        Opening = 100.00m, Additions = 5.00m, Depreciation = 10.00m, Disposals = 1.00m,
    };

    private static Problem NewProblem() => new()
    {
        Line = 3, LeaseId = "L-1", Field = "Opening", Code = "InvalidNumber", Message = "msg",
    };

    public static TheoryData<string> LeaseChanges() =>
        new() { "Line", "LeaseId", "AssetClass", "Description", "Currency", "Opening", "Additions", "Depreciation", "Disposals" };

    public static TheoryData<string> ProblemChanges() =>
        new() { "Line", "LeaseId", "LeaseIdNull", "Field", "FieldNull", "Code", "Message" };

    private static Lease Changed(Lease lease, string property)
    {
        switch (property)
        {
            case "Line": lease.Line++; break;
            case "LeaseId": lease.LeaseId = "l-1"; break;
            case "AssetClass": lease.AssetClass += "x"; break;
            case "Description": lease.Description += "x"; break;
            case "Currency": lease.Currency = "EUR"; break;
            case "Opening": lease.Opening += 0.01m; break;
            case "Additions": lease.Additions += 0.01m; break;
            case "Depreciation": lease.Depreciation += 0.01m; break;
            case "Disposals": lease.Disposals += 0.01m; break;
        }
        return lease;
    }

    private static Problem Changed(Problem problem, string property)
    {
        switch (property)
        {
            case "Line": problem.Line++; break;
            case "LeaseId": problem.LeaseId = "L-2"; break;
            case "LeaseIdNull": problem.LeaseId = null; break;
            case "Field": problem.Field = "Additions"; break;
            case "FieldNull": problem.Field = null; break;
            case "Code": problem.Code = "TooManyDecimals"; break;
            case "Message": problem.Message += "."; break;
        }
        return problem;
    }

    [Fact]
    public void Equal_models_pass_and_amount_scale_is_ignored()
    {
        var scaled = NewLease();
        scaled.Opening = 100m;
        AssertLease(NewLease(), scaled);
        AssertLeases([NewLease()], [scaled]);
        AssertProblem(NewProblem(), NewProblem());
        AssertProblems([NewProblem()], [NewProblem()]);
        AssertContainsProblem(NewProblem(), [Changed(NewProblem(), "Code"), NewProblem()]);
    }

    [Theory]
    [MemberData(nameof(LeaseChanges))]
    public void A_different_lease_property_fails(string property)
    {
        Assert.Throws<FailException>(() => AssertLease(NewLease(), Changed(NewLease(), property)));
        Assert.Throws<FailException>(() => AssertLeases([NewLease()], [Changed(NewLease(), property)]));
    }

    [Theory]
    [MemberData(nameof(ProblemChanges))]
    public void A_different_problem_property_fails(string property)
    {
        Assert.Throws<FailException>(() => AssertProblem(NewProblem(), Changed(NewProblem(), property)));
        Assert.Throws<FailException>(() => AssertProblems([NewProblem()], [Changed(NewProblem(), property)]));
        Assert.Throws<FailException>(() => AssertContainsProblem(NewProblem(), [Changed(NewProblem(), property)]));
    }

    [Fact]
    public void Lists_of_different_length_or_order_fail()
    {
        Assert.Throws<FailException>(() => AssertLeases([NewLease()], []));
        Assert.Throws<FailException>(() => AssertProblems([], [NewProblem()]));
        var other = Changed(NewProblem(), "Line");
        Assert.Throws<FailException>(() => AssertProblems([NewProblem(), other], [other, NewProblem()]));
        Assert.Throws<FailException>(() => AssertContainsProblem(NewProblem(), []));
    }
}
