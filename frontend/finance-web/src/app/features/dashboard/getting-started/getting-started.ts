import { Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { BankConnectionService } from '../../bank-connections/services/bank-connection.service';
import { BudgetsService } from '../../budgets/budgets.service';
import { SavingsGoalsService } from '../../savings-goals/savings-goals.service';

interface Step {
  title: string;
  description: string;
  link: string;
  action: string;
  done: boolean;
  optional?: boolean;
}

// First-run checklist on the dashboard. Hides itself once the required
// steps are done, or when dismissed (remembered per user in this browser).
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
  private readonly banks = inject(BankConnectionService);

  readonly accountCount = input.required<number>();
  readonly transactionCount = input.required<number>();

  private readonly counts = signal<{ budgets: number; goals: number; banks: number } | null>(null);
  readonly dismissed = signal(false);

  readonly steps = computed<Step[]>(() => {
    const counts = this.counts();
    return [
      {
        title: 'Add an account',
        description: 'Chequing, savings or a credit card, with its current balance.',
        link: '/accounts',
        action: 'Add account',
        done: this.accountCount() > 0
      },
      {
        title: 'Record your transactions',
        description: 'Add them by hand or import a CSV from your bank.',
        link: '/transactions',
        action: 'Add transactions',
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
      },
      {
        title: 'Connect your bank',
        description: 'Import transactions automatically (Bank plan).',
        link: '/bank-connections',
        action: 'Connect bank',
        done: (counts?.banks ?? 0) > 0,
        optional: true
      }
    ];
  });

  readonly completed = computed(() => this.steps().filter(step => step.done).length);

  readonly visible = computed(() =>
    this.counts() !== null &&
    !this.dismissed() &&
    this.steps().some(step => !step.done && !step.optional));

  ngOnInit(): void {
    this.dismissed.set(this.readDismissed());

    // A failing source only leaves its step unticked.
    forkJoin({
      budgets: this.budgets.getAll().pipe(catchError(() => of([]))),
      goals: this.goals.getAll().pipe(catchError(() => of([]))),
      banks: this.banks.getUserConnections().pipe(catchError(() => of([])))
    }).subscribe(result => this.counts.set({
      budgets: result.budgets.length,
      goals: result.goals.length,
      banks: result.banks.length
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
