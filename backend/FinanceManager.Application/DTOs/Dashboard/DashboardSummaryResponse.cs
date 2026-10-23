
using FinanceManager.Application.DTOs.Accounts;
using FinanceManager.Application.DTOs.Transactions;

namespace FinanceManager.Application.DTOs.Dashboard;

public record DashboardSummaryResponse(
    List<AccountBalanceResponse> Accounts,
    List<MonthlyCashFlowResponse> MonthlyCashFlow,
    List<CategorySpendingResponse> SpendingByCategory,
    List<TransactionResponse> RecentTransactions,
    List<UpcomingRecurringResponse> UpcomingRecurring
);
