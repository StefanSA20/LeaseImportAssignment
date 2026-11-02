namespace LeaseImport.Common;

public class Lease
{
    public int Line { get; set; }
    public string LeaseId { get; set; } = string.Empty;
    public string AssetClass { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal Opening { get; set; }
    public decimal Additions { get; set; }
    public decimal Depreciation { get; set; }
    public decimal Disposals { get; set; }
}

public class Problem
{
    public int Line { get; set; }
    public string? LeaseId { get; set; }
    public string? Field { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ImportResult
{
    public List<Lease> Leases { get; set; } = [];
    public List<Problem> Problems { get; set; } = [];
}
