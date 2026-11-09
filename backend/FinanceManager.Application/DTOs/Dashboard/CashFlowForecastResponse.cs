namespace FinanceManager.Application.DTOs.Dashboard;

public sealed record CashFlowForecastResponse(
    int HorizonDays,
    DateTime ForecastedAtUtc,
    List<ForecastCurrencyResponse> Currencies);

public sealed record ForecastCurrencyResponse(
    string Currency,
    decimal StartingBalance,
    decimal ProjectedEndingBalance,
    List<ForecastDayResponse> Days);

public sealed record ForecastDayResponse(
    DateOnly Date,
    decimal ExpectedIncome,
    decimal ExpectedExpenses,
    decimal NetCashFlow,
    decimal ProjectedBalance,
    int PlannedItems);
