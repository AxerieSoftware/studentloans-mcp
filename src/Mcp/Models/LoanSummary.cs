namespace Axerie.StudentLoans.Mcp.Models;

public sealed record LoanDetail(
    string? LoanId,
    string? LoanType,
    string? Servicer,
    decimal Principal,
    decimal Interest,
    decimal CapitalizedInterest,
    decimal LateFees,
    decimal TotalBalance);

public sealed record LoanSummary(
    string AccountId,
    string DisplayName,
    decimal TotalBalance,
    int LoanCount,
    IReadOnlyList<LoanDetail> Loans);
