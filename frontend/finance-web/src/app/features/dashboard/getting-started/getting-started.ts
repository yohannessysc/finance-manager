import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { BillingService, SubscriptionPlan } from '../../../core/billing/billing.service';
import { BudgetsService } from '../../budgets/budgets.service';
import { SavingsGoalsService } from '../../savings-goals/savings-goals.service';

interface Step {
  title: string;
  description: string;
  link: string;
  action: string;
  done: boolean;
}

// First-run guide for a new, empty workspace. It disappears for good once
// the user has an account and a transaction, or when dismissed
// (remembered per user in this browser).
@Component({
  selector: 'app-getting-started',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './getting-started.html',
  styleUrl: './getting-started.scss'
})
export class GettingStarted implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly budgets = inject(BudgetsService);
  private readonly goals = inject(SavingsGoalsService);
  private readonly billing = inject(BillingService);

  readonly accountCount = input.required<number>();
  readonly transactionCount = input.required<number>();

  private readonly counts = signal<{ budgets: number; goals: number; hasBankPlan: boolean } | null>(null);
  readonly dismissed = signal(false);

  readonly steps = computed<Step[]>(() => {
    const counts = this.counts();
    // Bank plan users get their accounts and transactions by connecting a bank.
    const bank = counts?.hasBankPlan ?? false;
    return [
      {
        title: bank ? 'Connect your bank' : 'Add an account',
        description: bank
          ? 'Your accounts and balances are imported automatically.'
          : 'Chequing, savings or a credit card, with its current balance.',
        link: bank ? '/bank-connections' : '/accounts',
        action: bank ? 'Connect bank' : 'Add account',
        done: this.accountCount() > 0
      },
      {
        title: bank ? 'Check your transactions' : 'Record your transactions',
        description: bank
          ? 'They sync from your bank. You can also add cash spending by hand.'
          : 'Add them by hand or import a CSV from your bank.',
        link: '/transactions',
        action: bank ? 'View transactions' : 'Add transactions',
        done: this.transactionCount() > 0
      },
      {
        title: 'Set a monthly budget',
        description: 'Pick a category and a limit to see where your money goes.',
        link: '/budgets',
        action: 'Create budget',
        done: (counts?.budgets ?? 0) > 0
      },
      {
        title: 'Create a savings goal',
        description: 'See what to save each month to hit a target on time.',
        link: '/savings-goals',
        action: 'Create goal',
        done: (counts?.goals ?? 0) > 0
      }
    ];
  });

  readonly completed = computed(() => this.steps().filter(step => step.done).length);

  // Only for new users: once there is an account with transactions the
  // dashboard has real data to show, so the guide gets out of the way.
  readonly visible = computed(() =>
    this.counts() !== null &&
    !this.dismissed() &&
    (this.accountCount() === 0 || this.transactionCount() === 0));

  ngOnInit(): void {
    this.dismissed.set(this.readDismissed());

    // A failing source only leaves its step unticked.
    forkJoin({
      budgets: this.budgets.getAll().pipe(catchError(() => of([]))),
      goals: this.goals.getAll().pipe(catchError(() => of([]))),
      plan: this.billing.getStatus().pipe(catchError(() => of(null)))
    }).subscribe(result => this.counts.set({
      budgets: result.budgets.length,
      goals: result.goals.length,
      hasBankPlan: (result.plan?.plan ?? SubscriptionPlan.Free) >= SubscriptionPlan.Bank
    }));
  }

  dismiss(): void {
    this.dismissed.set(true);
    try {
      localStorage.setItem(this.storageKey(), '1');
    } catch {
      // Storage unavailable (private mode): dismissal lasts this visit.
    }
  }

  private readDismissed(): boolean {
    try {
      return localStorage.getItem(this.storageKey()) === '1';
    } catch {
      return false;
    }
  }

  private storageKey(): string {
    return `fm.gettingStarted.dismissed.${this.auth.currentUser()?.id ?? 'anonymous'}`;
  }
}
