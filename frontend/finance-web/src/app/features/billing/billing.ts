import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { finalize, Observable } from 'rxjs';

import {
  BillingService,
  BillingStatus,
  SubscriptionPlan
} from '../../core/billing/billing.service';

interface PlanCard {
  plan: SubscriptionPlan;
  name: string;
  // Display only. Stripe prices are the source of truth: keep in sync.
  price: string;
  features: string[];
}

@Component({
  selector: 'app-billing',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './billing.html',
  styleUrl: './billing.scss'
})
export class Billing implements OnInit {
  private readonly billing = inject(BillingService);
  private readonly route = inject(ActivatedRoute);

  readonly Plan = SubscriptionPlan;
  readonly plans: PlanCard[] = [
    {
      plan: SubscriptionPlan.Free,
      name: 'Free',
      price: '$0',
      features: ['Manual accounts and transactions', 'CSV import', 'Budgets and savings goals', 'Household sharing']
    },
    {
      plan: SubscriptionPlan.Bank,
      name: 'Bank',
      price: '$8.99/month',
      features: ['Everything in Free', 'Automatic bank connections', 'Transactions sync every 15 minutes', 'Linked-account savings goals']
    },
    {
      plan: SubscriptionPlan.Premium,
      name: 'Premium',
      price: '$14.99/month',
      features: ['Everything in Bank', 'AI insights and suggestions (coming soon)']
    }
  ];

  readonly status = signal<BillingStatus | null>(null);
  readonly loading = signal(true);
  readonly redirecting = signal(false);
  readonly error = signal('');
  readonly notice = signal('');

  ngOnInit(): void {
    const checkout = this.route.snapshot.queryParamMap.get('checkout');
    if (checkout === 'success') {
      this.notice.set('Thanks! Your plan updates as soon as Stripe confirms the payment, usually within a few seconds.');
    } else if (checkout === 'cancelled') {
      this.notice.set('Checkout cancelled. You have not been charged.');
    }

    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.billing.getStatus()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: status => this.status.set(status),
        error: (error: HttpErrorResponse) =>
          this.error.set(this.getError(error, 'Unable to load your plan.'))
      });
  }

  // Paid subscribers change plans in the Stripe portal, which handles
  // proration; checkout is only for starting a subscription.
  hasPaidSubscription(): boolean {
    const status = this.status();
    return !!status && status.subscribedPlan !== null &&
      status.subscriptionStatus !== 'canceled' &&
      status.subscriptionStatus !== 'incomplete_expired';
  }

  choose(plan: SubscriptionPlan): void {
    this.redirect(this.billing.checkout(plan));
  }

  manage(): void {
    this.redirect(this.billing.portal());
  }

  private redirect(request: Observable<{ url: string }>): void {
    this.redirecting.set(true);
    this.error.set('');

    request.subscribe({
      next: result => window.location.assign(result.url),
      error: (error: HttpErrorResponse) => {
        this.redirecting.set(false);
        this.error.set(this.getError(error, 'Unable to open billing. Please try again.'));
      }
    });
  }

  private getError(error: HttpErrorResponse, fallback: string): string {
    return typeof error.error?.message === 'string' ? error.error.message : fallback;
  }
}
