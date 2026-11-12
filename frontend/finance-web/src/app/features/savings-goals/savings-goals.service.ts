import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { RecurrenceFrequency } from '../recurring/recurring.service';

export enum SavingsGoalType {
  Custom = 1,
  EmergencyFund = 2
}

export enum SavingsGoalStatus {
  NoTargetDate = 1,
  OnTrack = 2,
  Behind = 3,
  Completed = 4
}

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
  householdId: string | null;
  canEdit: boolean;
  goalType: SavingsGoalType;
  emergencyFundMonths: number | null;
  averageMonthlyExpenses: number | null;
  linkedAccountId: string | null;
  linkedAccountName: string | null;
  autoContributionAmount: number | null;
  autoContributionFrequency: RecurrenceFrequency | null;
  autoContributionStartUtc: string | null;
  nextAutoContributionUtc: string | null;
  notificationsEnabled: boolean;
  status: SavingsGoalStatus;
  monthlyPace: number;
  monthlyContributionNeeded: number | null;
  projectedCompletionDateUtc: string | null;
}

export interface SavingsGoalContribution {
  id: string;
  savingsGoalId: string;
  amount: number;
  contributionDateUtc: string;
  note: string | null;
  createdAtUtc: string;
  contributorUserId: string;
  contributorEmail: string | null;
  isScheduled: boolean;
}

// Settings shared by create and update. Update is a full replacement,
// so omitted (null) settings are cleared on the server.
export interface SavingsGoalSettings {
  emergencyFundMonths: number | null;
  linkedAccountId: string | null;
  autoContributionAmount: number | null;
  autoContributionFrequency: RecurrenceFrequency | null;
  autoContributionStartUtc: string | null;
  notificationsEnabled: boolean;
}

export interface SavingsGoalUpdateRequest extends SavingsGoalSettings {
  name: string;
  targetAmount: number;
  targetDateUtc: string | null;
}

export interface SavingsGoalUpsertRequest extends SavingsGoalUpdateRequest {
  currency: string;
  householdId: string | null;
  goalType: SavingsGoalType;
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
