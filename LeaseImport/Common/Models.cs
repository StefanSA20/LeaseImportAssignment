using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("LeaseImport.Tests")]

namespace LeaseImport.Common;

internal sealed record Lease(
    int Line,
    string LeaseId,
    string AssetClass,
    string Description,
    string Currency,
    decimal Opening,
    decimal Additions,
    decimal Depreciation,
    decimal Disposals);

internal sealed record Problem(
    int Line,
    string? LeaseId,
    string? Field,
    string Code,
    string Message);

internal sealed record ImportResult(
    IReadOnlyList<Lease> Leases,
    IReadOnlyList<Problem> Problems);
