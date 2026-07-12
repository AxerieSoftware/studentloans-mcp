namespace Axerie.StudentLoans.Mcp.Models;

public sealed record LoanSummary(
    Guid AccountId,
    string DisplayName,
    decimal TotalBalance,
    decimal TotalPrincipal,
    decimal TotalAccruedInterest,
    decimal? WeightedAverageInterestRate,
    int LoanCount,
    IReadOnlyList<LoanDetail> Loans);

public sealed record LoanDetail(
    string? LoanId,
    string? LoanType,
    string? Servicer,
    decimal? InterestRate,
    decimal Principal,
    decimal Interest,
    decimal TotalBalance,
    string? Status,
    DateOnly? BalanceAsOfDate,
    DateOnly? NextPaymentDueDate,
    string? RepaymentPlanType,
    decimal? RepaymentPlanScheduledAmount,
    DateOnly? DelinquencyDate,
    int? PslfCumulativeMatchedMonths);
