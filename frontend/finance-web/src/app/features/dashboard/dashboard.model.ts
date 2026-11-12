
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
  // Present when split across several categories.
  splits?: TransactionSplit[] | null;
  attachmentCount?: number;
}

export interface TransactionSplit {
  id: string;
  categoryId: string;
  amount: number;
  note: string | null;
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

export interface ForecastDay {
  date: string;
  expectedIncome: number;
  expectedExpenses: number;
  netCashFlow: number;
  projectedBalance: number;
  plannedItems: number;
}

export interface ForecastCurrency {
  currency: string;
  startingBalance: number;
  projectedEndingBalance: number;
  days: ForecastDay[];
}

export interface CashFlowForecast {
  horizonDays: number;
  forecastedAtUtc: string;
  currencies: ForecastCurrency[];
}
