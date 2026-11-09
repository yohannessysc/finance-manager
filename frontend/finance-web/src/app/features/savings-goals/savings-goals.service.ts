import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface SavingsGoal {
  id: string;
  name: string;
  currency: string;
  targetAmount: number;
  amountSaved: number;
  remaining: number;
  progressPercent: number;
  targetDateUtc: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface SavingsGoalContribution {
  id: string;
  savingsGoalId: string;
  amount: number;
  contributionDateUtc: string;
  note: string | null;
  createdAtUtc: string;
}

export interface SavingsGoalUpsertRequest {
  name: string;
  currency: string;
  targetAmount: number;
  targetDateUtc: string | null;
}

export interface SavingsGoalUpdateRequest {
  name: string;
  targetAmount: number;
  targetDateUtc: string | null;
}

export interface SavingsGoalContributionRequest {
  amount: number;
  contributionDateUtc: string;
  note: string;
}

@Injectable({
  providedIn: 'root'
})
export class SavingsGoalsService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/savings-goals';

  getAll(): Observable<SavingsGoal[]> {
    return this.http.get<SavingsGoal[]>(this.url);
  }

  create(request: SavingsGoalUpsertRequest): Observable<SavingsGoal> {
    return this.http.post<SavingsGoal>(this.url, request);
  }

  update(
    id: string,
    request: SavingsGoalUpdateRequest
  ): Observable<SavingsGoal> {
    return this.http.put<SavingsGoal>(`${this.url}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  getContributions(id: string): Observable<SavingsGoalContribution[]> {
    return this.http.get<SavingsGoalContribution[]>(
      `${this.url}/${id}/contributions`
    );
  }

  addContribution(
    id: string,
    request: SavingsGoalContributionRequest
  ): Observable<SavingsGoal> {
    return this.http.post<SavingsGoal>(
      `${this.url}/${id}/contributions`,
      request
    );
  }

  deleteContribution(
    goalId: string,
    contributionId: string
  ): Observable<void> {
    return this.http.delete<void>(
      `${this.url}/${goalId}/contributions/${contributionId}`
    );
  }
}
