import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface CategoryReportRow {
  categoryId: string | null;
  name: string;
  // 1 = Income, 2 = Expense
  type: number;
  total: number;
  byMonth: number[];
  children: CategoryReportRow[];
}

export interface CategoryReport {
  currency: string;
  currencies: string[];
  // "yyyy-MM"; every byMonth array lines up with it.
  months: string[];
  incomeByMonth: number[];
  expensesByMonth: number[];
  categories: CategoryReportRow[];
}

@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly http = inject(HttpClient);

  getCategoryReport(from: string, to: string, currency?: string | null): Observable<CategoryReport> {
    let params = new HttpParams().set('from', from).set('to', to);
    if (currency) params = params.set('currency', currency);
    return this.http.get<CategoryReport>('/api/v1/reports/categories', { params });
  }
}
