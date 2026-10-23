
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { RecentTransaction } from '../dashboard/dashboard.model';

export enum TransactionType {
  Income = 1,
  Expense = 2,
  Transfer = 3
}

export interface TransactionCategory {
  id: string;
  name: string;
  type: TransactionType;
  createdAtUtc: string;
}

export interface SaveTransactionRequest {
  financialAccountId: string;
  destinationAccountId: string | null;
  categoryId: string | null;
  type: TransactionType;
  amount: number;
  description: string;
  merchant: string | null;
  transactionDateUtc: string;
}

@Injectable({
  providedIn: 'root'
})
export class TransactionsService {
  private readonly http = inject(HttpClient);

  private readonly transactionsUrl = '/api/v1/transactions';
  private readonly categoriesUrl = '/api/v1/categories';

  getTransactions(): Observable<RecentTransaction[]> {
    return this.http.get<RecentTransaction[]>(
      this.transactionsUrl
    );
  }

  getCategories(): Observable<TransactionCategory[]> {
    return this.http.get<TransactionCategory[]>(
      this.categoriesUrl
    );
  }

  create(
    request: SaveTransactionRequest
  ): Observable<RecentTransaction> {
    return this.http.post<RecentTransaction>(
      this.transactionsUrl,
      request
    );
  }

  update(
    id: string,
    request: SaveTransactionRequest
  ): Observable<RecentTransaction> {
    return this.http.put<RecentTransaction>(
      `${this.transactionsUrl}/${id}`,
      request
    );
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(
      `${this.transactionsUrl}/${id}`
    );
  }
}
