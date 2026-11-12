import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

// Ordered: a higher plan includes everything in the lower ones.
export enum SubscriptionPlan {
  Free = 0,
  Bank = 1,
  Premium = 2
}

export enum BillingInterval {
  Monthly = 1,
  Yearly = 2
}

export interface BillingStatus {
  plan: SubscriptionPlan;
  subscribedPlan: SubscriptionPlan | null;
  subscriptionStatus: string | null;
  currentPeriodEndUtc: string | null;
  cancelAtPeriodEnd: boolean;
  hasBillingAccount: boolean;
  billingConfigured: boolean;
}

interface BillingRedirect {
  url: string;
}

@Injectable({
  providedIn: 'root'
})
export class BillingService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/billing';

  getStatus(): Observable<BillingStatus> {
    return this.http.get<BillingStatus>(this.url);
  }

  checkout(
    plan: SubscriptionPlan,
    interval: BillingInterval = BillingInterval.Monthly
  ): Observable<BillingRedirect> {
    return this.http.post<BillingRedirect>(`${this.url}/checkout`, { plan, interval });
  }

  portal(): Observable<BillingRedirect> {
    return this.http.post<BillingRedirect>(`${this.url}/portal`, {});
  }
}
