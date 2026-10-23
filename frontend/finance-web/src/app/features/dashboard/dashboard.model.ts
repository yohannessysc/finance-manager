
export interface AccountBalance {
  accountId: string;
  accountName: string;
  currency: string;
  openingBalance: number;
  totalIncome: number;
  totalExpenses: number;
  incomingTransfers: number;
  outgoingTransfers: number;
  currentBalance: number;
}

export interface MonthlyCashFlow {
  year: number;
  month: number;
  currency: string;
  totalIncome: number;
  totalExpenses: number;
  netCashFlow: number;
}

export interface CategorySpending {
  categoryId: string | null;
  categoryName: string;
  currency: string;
  amount: number;
  percentage: number;
}

export interface RecentTransaction {
  id: string;
  financialAccountId: string;
  destinationAccountId: string | null;
  categoryId: string | null;
  recurringTransactionId: string | null;
  type: number;
  amount: number;
  description: string;
  merchant: string | null;
  transactionDateUtc: string;
  createdAtUtc: string;
}

export interface UpcomingRecurring {
  id: string;
  description: string;
  type: number;
  amount: number;
  currency: string;
  nextOccurrenceUtc: string;
  timeZoneId: string;
}

export interface DashboardSummary {
  accounts: AccountBalance[];
  monthlyCashFlow: MonthlyCashFlow[];
  spendingByCategory: CategorySpending[];
  recentTransactions: RecentTransaction[];
  upcomingRecurring: UpcomingRecurring[];
}
