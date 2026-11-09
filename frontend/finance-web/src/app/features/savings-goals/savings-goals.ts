import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { forkJoin, finalize } from 'rxjs';

import { Accounts } from '../accounts/accounts';
import {
  SavingsGoal,
  SavingsGoalContribution,
  SavingsGoalsService
} from './savings-goals.service';

@Component({
  selector: 'app-savings-goals',
  standalone: true,
  imports: [ReactiveFormsModule, CurrencyPipe, DatePipe, DecimalPipe],
  templateUrl: './savings-goals.html',
  styleUrl: './savings-goals.scss'
})
export class SavingsGoals implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly goalsService = inject(SavingsGoalsService);
  private readonly accountsService = inject(Accounts);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly goals = signal<SavingsGoal[]>([]);
  readonly currencies = signal<string[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly selectedGoalId = signal<string | null>(null);
  readonly contributions = signal<SavingsGoalContribution[]>([]);

  readonly goalForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    currency: ['', Validators.required],
    targetAmount: [0, [Validators.required, Validators.min(0.01)]],
    targetDate: ['']
  });

  readonly contributionForm = this.fb.nonNullable.group({
    amount: [0, [Validators.required, Validators.min(0.01)]],
    date: [new Date().toISOString().slice(0, 10), Validators.required],
    note: ['', Validators.maxLength(250)]
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');

    forkJoin({
      goals: this.goalsService.getAll(),
      accounts: this.accountsService.getAccounts()
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: result => {
          this.goals.set(result.goals);
          this.currencies.set([
            ...new Set(result.accounts.map(account => account.currency))
          ].sort());

          if (!this.goalForm.controls.currency.value) {
            this.goalForm.controls.currency.setValue(
              this.currencies()[0] ?? ''
            );
          }
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(this.getError(error, 'Unable to load savings goals.'));
        }
      });
  }

  saveGoal(): void {
    if (this.saving()) return;

    if (this.goalForm.invalid) {
      this.goalForm.markAllAsTouched();
      return;
    }

    const values = this.goalForm.getRawValue();
    const targetDateUtc = values.targetDate
      ? new Date(`${values.targetDate}T00:00:00.000Z`).toISOString()
      : null;
    const id = this.editingId();
    this.saving.set(true);
    this.error.set('');
    this.message.set('');

    const request = id
      ? this.goalsService.update(id, {
          name: values.name.trim(),
          targetAmount: values.targetAmount,
          targetDateUtc
        })
      : this.goalsService.create({
          name: values.name.trim(),
          currency: values.currency,
          targetAmount: values.targetAmount,
          targetDateUtc
        });

    request
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: goal => {
          this.goals.update(items =>
            id
              ? items.map(item => item.id === id ? goal : item)
              : [...items, goal]
          );
          this.message.set(id ? 'Savings goal updated.' : 'Savings goal created.');
          this.cancelEdit();
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(this.getError(error, 'Unable to save this goal.'));
        }
      });
  }

  edit(goal: SavingsGoal): void {
    this.editingId.set(goal.id);
    this.goalForm.setValue({
      name: goal.name,
      currency: goal.currency,
      targetAmount: goal.targetAmount,
      targetDate: goal.targetDateUtc?.slice(0, 10) ?? ''
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.goalForm.reset({
      name: '',
      currency: this.currencies()[0] ?? '',
      targetAmount: 0,
      targetDate: ''
    });
  }

  removeGoal(goal: SavingsGoal): void {
    this.goalsService.delete(goal.id).subscribe({
      next: () => {
        this.goals.update(items => items.filter(item => item.id !== goal.id));
        if (this.selectedGoalId() === goal.id) {
          this.selectedGoalId.set(null);
          this.contributions.set([]);
        }
        if (this.editingId() === goal.id) this.cancelEdit();
        this.message.set(`${goal.name} deleted.`);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(this.getError(error, 'Unable to delete this goal.'));
      }
    });
  }

  showContributions(goal: SavingsGoal): void {
    this.selectedGoalId.set(goal.id);
    this.error.set('');

    this.goalsService.getContributions(goal.id).subscribe({
      next: items => this.contributions.set(items),
      error: (error: HttpErrorResponse) => {
        this.error.set(this.getError(error, 'Unable to load contributions.'));
      }
    });
  }

  addContribution(goal: SavingsGoal): void {
    if (this.saving()) return;

    if (this.contributionForm.invalid) {
      this.contributionForm.markAllAsTouched();
      return;
    }

    const value = this.contributionForm.getRawValue();
    const contributionDateUtc =
      new Date(`${value.date}T12:00:00.000Z`).toISOString();
    this.saving.set(true);
    this.error.set('');

    this.goalsService.addContribution(goal.id, {
      amount: value.amount,
      contributionDateUtc,
      note: value.note.trim()
    })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: updated => {
          this.replaceGoal(updated);
          this.contributionForm.reset({
            amount: 0,
            date: new Date().toISOString().slice(0, 10),
            note: ''
          });
          this.showContributions(updated);
          this.message.set('Contribution added.');
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(this.getError(error, 'Unable to add this contribution.'));
        }
      });
  }

  removeContribution(
    goal: SavingsGoal,
    contribution: SavingsGoalContribution
  ): void {
    this.goalsService.deleteContribution(goal.id, contribution.id)
      .subscribe({
        next: () => {
          this.contributions.update(items =>
            items.filter(item => item.id !== contribution.id)
          );
          this.load();
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(this.getError(error, 'Unable to remove contribution.'));
        }
      });
  }

  barWidth(goal: SavingsGoal): number {
    return Math.max(0, Math.min(100, goal.progressPercent));
  }

  private replaceGoal(updated: SavingsGoal): void {
    this.goals.update(items =>
      items.map(item => item.id === updated.id ? updated : item)
    );
  }

  private getError(error: HttpErrorResponse, fallback: string): string {
    if (error.status === 401)
      return 'Your session has expired. Please sign in again.';

    if (typeof error.error?.message === 'string')
      return error.error.message;

    return fallback;
  }
}
