
import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';

import { TransactionType } from '../transactions/transactions.service';

export enum RecurrenceFrequency {
  Daily = 1,
  Weekly = 2,
  Biweekly = 3,
  Monthly = 4,
  Yearly = 5
}

export enum RecurringStatus {
  Active = 1,
  Paused = 2,
  Cancelled = 3
}

export interface RecurringTransaction {
  id: string;
  financialAccountId: string;
  destinationAccountId: string | null;
  categoryId: string | null;
  type: TransactionType;
  amount: number;
  description: string;
  frequency: RecurrenceFrequency;
  status: RecurringStatus;
  startDateUtc: string;
  nextOccurrenceUtc: string;
  endDateUtc: string | null;
  createdAtUtc: string;
  timeZoneId: string;
}

export interface SaveRecurringRequest {
  financialAccountId: string;
  destinationAccountId: string | null;
  categoryId: string | null;
  type: TransactionType;
  amount: number;
  description: string;
  frequency: RecurrenceFrequency;
  startDateUtc: string;
  endDateUtc: string | null;
  timeZoneId: string;
}

@Injectable({ providedIn: 'root' })
export class RecurringService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/recurring-transactions';

  getAll(): Observable<RecurringTransaction[]> {
    return this.http.get<RecurringTransaction[]>(this.url);
  }

  create(request: SaveRecurringRequest): Observable<RecurringTransaction> {
    return this.http.post<RecurringTransaction>(this.url, request);
  }

  update(
    id: string,
    request: SaveRecurringRequest
  ): Observable<RecurringTransaction> {
    return this.http.put<RecurringTransaction>(
      `${this.url}/${id}`,
      request
    );
  }

  pause(id: string): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/pause`, {});
  }

  resume(id: string): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/resume`, {});
  }

  cancel(id: string): Observable<void> {
    return this.http.post<void>(`${this.url}/${id}/cancel`, {});
  }
}
