
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
  parentCategoryId?: string | null;
}

export interface SplitLine {
  categoryId: string;
  amount: number;
  note: string | null;
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

export interface CsvImportError {
  row: number;
  message: string;
}

export interface CsvImportResult {
  imported: number;
  duplicates: number;
  invalidRows: number;
  errors: CsvImportError[];
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

  bulkSetCategory(transactionIds: string[], categoryId: string | null): Observable<{ updated: number; skipped: number }> {
    return this.http.post<{ updated: number; skipped: number }>(
      `${this.transactionsUrl}/bulk-category`,
      { transactionIds, categoryId }
    );
  }

  // An empty list removes the split.
  setSplits(id: string, lines: SplitLine[]): Observable<RecentTransaction> {
    return this.http.put<RecentTransaction>(
      `${this.transactionsUrl}/${id}/splits`,
      { lines }
    );
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(
      `${this.transactionsUrl}/${id}`
    );
  }

  // OFX/QFX statements go into one chosen account.
  importOfx(file: File, accountId: string): Observable<CsvImportResult> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('accountId', accountId);

    return this.http.post<CsvImportResult>(
      `${this.transactionsUrl}/import/ofx`,
      formData
    );
  }

  importCsv(file: File): Observable<CsvImportResult> {
    const formData = new FormData();
    formData.append('file', file);

    return this.http.post<CsvImportResult>(
      `${this.transactionsUrl}/import/csv`,
      formData
    );
  }
}
