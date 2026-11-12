import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';

import { CategoryReport, CategoryReportRow, ReportsService } from './reports.service';

type Period = 'last3' | 'last6' | 'last12' | 'thisYear' | 'lastYear';
type View = 'expense' | 'income';

const EXPENSE = 2;
const INCOME = 1;

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CurrencyPipe, DecimalPipe],
  templateUrl: './reports.html',
  styleUrl: './reports.scss'
})
export class Reports implements OnInit {
  private readonly service = inject(ReportsService);

  readonly periods: { value: Period; label: string }[] = [
    { value: 'last3', label: 'Last 3 months' },
    { value: 'last6', label: 'Last 6 months' },
    { value: 'last12', label: 'Last 12 months' },
    { value: 'thisYear', label: 'This year' },
    { value: 'lastYear', label: 'Last year' }
  ];

  readonly period = signal<Period>('last6');
  readonly view = signal<View>('expense');
  readonly currency = signal<string | null>(null);
  readonly report = signal<CategoryReport | null>(null);
  readonly loading = signal(true);
  readonly error = signal('');
  readonly expanded = signal<Set<string>>(new Set());

  readonly totalIncome = computed(() => this.sum(this.report()?.incomeByMonth));
  readonly totalExpenses = computed(() => this.sum(this.report()?.expensesByMonth));
  readonly net = computed(() => this.totalIncome() - this.totalExpenses());
  readonly monthCount = computed(() => this.report()?.months.length ?? 0);

  readonly rows = computed(() =>
    (this.report()?.categories ?? []).filter(row => row.type === (this.view() === 'income' ? INCOME : EXPENSE)));

  readonly viewTotal = computed(() => (this.view() === 'income' ? this.totalIncome() : this.totalExpenses()));

  // Bar heights relative to the largest monthly amount.
  readonly chartMax = computed(() => {
    const report = this.report();
    return Math.max(1, ...(report?.incomeByMonth ?? []), ...(report?.expensesByMonth ?? []));
  });

  ngOnInit(): void {
    this.load();
  }

  setPeriod(value: string): void {
    this.period.set(value as Period);
    this.load();
  }

  setCurrency(value: string): void {
    this.currency.set(value);
    this.load();
  }

  load(): void {
    const { from, to } = this.range(this.period());
    this.loading.set(true);
    this.error.set('');

    this.service.getCategoryReport(from, to, this.currency()).subscribe({
      next: report => {
        this.report.set(report);
        this.currency.set(report.currency);
        this.loading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.error.set(err.error?.message ?? 'Unable to load the report. Please try again.');
        this.loading.set(false);
      }
    });
  }

  toggle(row: CategoryReportRow): void {
    const key = this.key(row);
    this.expanded.update(set => {
      const next = new Set(set);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  isExpanded(row: CategoryReportRow): boolean {
    return this.expanded().has(this.key(row));
  }

  share(amount: number): number {
    const total = this.viewTotal();
    return total === 0 ? 0 : (amount / total) * 100;
  }

  barHeight(amount: number): number {
    return (amount / this.chartMax()) * 100;
  }

  monthLabel(month: string, withYear = false): string {
    const [year, mon] = month.split('-').map(Number);
    return new Date(year, mon - 1, 1).toLocaleDateString(undefined, {
      month: 'short',
      ...(withYear ? { year: 'numeric' } : {})
    });
  }

  // Spreadsheet-friendly CSV of the current view (opens in Excel/Numbers).
  exportCsv(): void {
    const report = this.report();
    if (!report) return;

    const header = ['Category', 'Subcategory', ...report.months, 'Total'];
    const lines = [header];
    for (const row of this.rows()) {
      lines.push([row.name, '', ...row.byMonth.map(String), String(row.total)]);
      for (const child of row.children) {
        lines.push([row.name, child.name, ...child.byMonth.map(String), String(child.total)]);
      }
    }

    const csv = lines.map(line => line.map(cell => this.csvCell(cell)).join(',')).join('\r\n');
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = `${this.view() === 'income' ? 'income' : 'spending'}-${report.months[0]}-to-${report.months.at(-1)}-${report.currency}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }

  private csvCell(value: string): string {
    // Quote, and neutralize spreadsheet formulas in category names.
    const safe = /^[=+\-@]/.test(value) && Number.isNaN(Number(value)) ? `'${value}` : value;
    return `"${safe.replace(/"/g, '""')}"`;
  }

  private key(row: CategoryReportRow): string {
    return `${row.type}:${row.categoryId ?? 'none'}`;
  }

  private sum(values: number[] | undefined): number {
    return (values ?? []).reduce((total, value) => total + value, 0);
  }

  private range(period: Period): { from: string; to: string } {
    const now = new Date();
    const month = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}`;
    const monthsBack = (count: number) => new Date(now.getFullYear(), now.getMonth() - (count - 1), 1);

    switch (period) {
      case 'last3': return { from: month(monthsBack(3)), to: month(now) };
      case 'last6': return { from: month(monthsBack(6)), to: month(now) };
      case 'last12': return { from: month(monthsBack(12)), to: month(now) };
      case 'thisYear': return { from: `${now.getFullYear()}-01`, to: month(now) };
      case 'lastYear': return { from: `${now.getFullYear() - 1}-01`, to: `${now.getFullYear() - 1}-12` };
    }
  }
}
