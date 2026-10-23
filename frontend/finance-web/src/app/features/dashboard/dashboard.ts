
import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { DashboardService } from './dashboard.service';
import {
  AccountBalance,
  DashboardSummary,
  MonthlyCashFlow,
  RecentTransaction
} from './dashboard.model';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, DecimalPipe, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  private readonly dashboardService = inject(DashboardService);

  readonly loading = signal(true);
  readonly error = signal('');
  readonly data = signal<DashboardSummary | null>(null);
  readonly selectedCurrency = signal('CAD');

  readonly currencies = computed(() => {
    const summary = this.data();

    if (!summary) return ['CAD'];

    const found = [
      ...summary.accounts.map(x => x.currency),
      ...summary.monthlyCashFlow.map(x => x.currency)
    ];

    return [...new Set(found)].sort();
  });

  readonly accounts = computed(() =>
    this.data()?.accounts.filter(
      x => x.currency === this.selectedCurrency()
    ) ?? []
  );

  readonly totalBalance = computed(() =>
    this.accounts().reduce(
      (total, account) => total + account.currentBalance, 0
    )
  );

  readonly monthlyTrend = computed(() =>
    this.data()?.monthlyCashFlow
      .filter(x => x.currency === this.selectedCurrency())
      .sort((a, b) =>
        (a.year * 12 + a.month) - (b.year * 12 + b.month)
      ) ?? []
  );

  readonly currentMonth = computed<MonthlyCashFlow | null>(() => {
    const trend = this.monthlyTrend();
    return trend.length ? trend[trend.length - 1] : null;
  });

  readonly spending = computed(() =>
    this.data()?.spendingByCategory
      .filter(x => x.currency === this.selectedCurrency())
      .sort((a, b) => b.amount - a.amount) ?? []
  );

  readonly transactions = computed(() => {
    const summary = this.data();
    if (!summary) return [];

    const accountIds = new Set(
      this.accounts().map(x => x.accountId)
    );

    return summary.recentTransactions.filter(
      x => accountIds.has(x.financialAccountId)
    );
  });

  readonly upcoming = computed(() =>
    this.data()?.upcomingRecurring.filter(
      x => x.currency === this.selectedCurrency()
    ) ?? []
  );

  readonly chartMax = computed(() => {
    const trend = this.monthlyTrend();

    return Math.max(
      1,
      ...trend.map(x =>
        Math.max(x.totalIncome, x.totalExpenses)
      )
    );
  });

  ngOnInit(): void {
    this.loadDashboard();
  }

  loadDashboard(): void {
    this.loading.set(true);
    this.error.set('');

    this.dashboardService.getSummary().subscribe({
      next: summary => {
        this.data.set(summary);

        const available = [
          ...new Set(
            summary.accounts.map(x => x.currency)
          )
        ];

        if (
          available.length &&
          !available.includes(this.selectedCurrency())
        ) {
          this.selectedCurrency.set(available[0]);
        }

        this.loading.set(false);
      },
      error: () => {
        this.error.set(
          'Could not load your financial dashboard. Please try again.'
        );
        this.loading.set(false);
      }
    });
  }

  setCurrency(currency: string): void {
    this.selectedCurrency.set(currency);
  }

  barHeight(amount: number): number {
    return Math.max(0, (amount / this.chartMax()) * 100);
  }

  monthLabel(month: number): string {
    return new Date(2026, month - 1, 1)
      .toLocaleString('en-CA', { month: 'short' });
  }

  transactionLabel(transaction: RecentTransaction): string {
    if (transaction.type === 1) return 'Income';
    if (transaction.type === 2) return 'Expense';
    return 'Transfer';
  }

  transactionSign(transaction: RecentTransaction): string {
    return transaction.type === 1 ? '+' :
      transaction.type === 2 ? '−' : '';
  }

  accountName(transaction: RecentTransaction): string {
    return this.data()?.accounts.find(
      a => a.accountId === transaction.financialAccountId
    )?.accountName ?? 'Account';
  }

  trackAccount(_: number, account: AccountBalance): string {
    return account.accountId;
  }
}
