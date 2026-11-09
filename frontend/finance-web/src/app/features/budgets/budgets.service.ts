import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface Budget {
  id: string;
  categoryId: string;
  categoryName: string;
  currency: string;
  monthlyLimit: number;
  amountSpent: number;
  remaining: number;
  percentSpent: number;
  periodStartUtc: string;
  periodEndUtc: string;
  periodStartDate: string;
  periodEndDate: string;
  householdId: string | null;
  canEdit: boolean;
}

export interface BudgetUpsertRequest {
  categoryId: string;
  currency: string;
  monthlyLimit: number;
  householdId?: string | null;
}

@Injectable({
  providedIn: 'root'
})
export class BudgetsService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/budgets';

  getAll(): Observable<Budget[]> {
    return this.http.get<Budget[]>(this.url);
  }

  create(request: BudgetUpsertRequest): Observable<Budget> {
    return this.http.post<Budget>(this.url, request);
  }

  update(id: string, request: BudgetUpsertRequest): Observable<Budget> {
    return this.http.put<Budget>(`${this.url}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
