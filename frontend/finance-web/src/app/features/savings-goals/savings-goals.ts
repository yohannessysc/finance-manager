import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { forkJoin, finalize } from 'rxjs';

import { Accounts } from '../accounts/accounts';
import { FinancialAccount } from '../accounts/financial-account.model';
import {
  Household,
  HouseholdsService
} from '../households/households.service';
import { RecurrenceFrequency } from '../recurring/recurring.service';
import {
  SavingsGoal,
  SavingsGoalContribution,
  SavingsGoalSettings,
  SavingsGoalsService,
  SavingsGoalStatus,
  SavingsGoalType
} from './savings-goals.service';

type TrackingMode = 'manual' | 'linked';
type GoalFormValue = ReturnType<SavingsGoals['goalForm']['getRawValue']>;

const STATUS_LABELS: Record<SavingsGoalStatus, string> = {
  [SavingsGoalStatus.NoTargetDate]: 'No target date',
  [SavingsGoalStatus.OnTrack]: 'On track',
  [SavingsGoalStatus.Behind]: 'Behind',
  [SavingsGoalStatus.Completed]: 'Completed'
};

const FREQUENCY_LABELS: Record<RecurrenceFrequency, string> = {
  [RecurrenceFrequency.Daily]: 'Daily',
  [RecurrenceFrequency.Weekly]: 'Weekly',
  [RecurrenceFrequency.Biweekly]: 'Every 2 weeks',
  [RecurrenceFrequency.Monthly]: 'Monthly',
  [RecurrenceFrequency.Yearly]: 'Yearly'
};

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
  private readonly householdsService = inject(HouseholdsService);

  readonly GoalType = SavingsGoalType;
  readonly GoalStatus = SavingsGoalStatus;
  readonly frequencies = Object.entries(FREQUENCY_LABELS)
    .map(([value, label]) => ({ value: Number(value) as RecurrenceFrequency, label }));

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly goals = signal<SavingsGoal[]>([]);
  readonly accounts = signal<FinancialAccount[]>([]);
  readonly households = signal<Household[]>([]);
  readonly currencies = signal<string[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly selectedGoalId = signal<string | null>(null);
  readonly contributions = signal<SavingsGoalContribution[]>([]);

  readonly goalForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    householdId: [''],
    goalType: [SavingsGoalType.Custom],
    currency: ['', Validators.required],
    emergencyFundMonths: [6, [Validators.min(1), Validators.max(24)]],
    targetAmount: [0, [Validators.required, Validators.min(0.01)]],
    targetDate: [''],
    tracking: ['manual' as TrackingMode],
    linkedAccountId: [''],
    autoEnabled: [false],
    autoAmount: [0, Validators.min(0)],
    autoFrequency: [RecurrenceFrequency.Monthly],
    autoStart: [today()],
    notificationsEnabled: [true]
  });

  readonly contributionForm = this.fb.nonNullable.group({
    amount: [0, [Validators.required, Validators.min(0.01)]],
    date: [today(), Validators.required],
    note: ['', Validators.maxLength(250)]
  });

  private readonly formValue = toSignal(this.goalForm.valueChanges, {
    initialValue: this.goalForm.getRawValue()
  });

  readonly editableHouseholds = computed(() =>
    this.households().filter(household => household.canEdit)
  );

  // Accounts a goal in the form's scope and currency may link to.
  // Each account backs at most one goal.
  readonly linkableAccounts = computed(() => {
    const form = this.formValue();
    const editing = this.goals().find(goal => goal.id === this.editingId());
    const householdId = editing ? editing.householdId : form.householdId || null;
    const currency = editing ? editing.currency : form.currency;
    const taken = new Set(this.goals()
      .filter(goal => goal.id !== editing?.id && goal.linkedAccountId)
      .map(goal => goal.linkedAccountId));

    return this.accounts().filter(account =>
      account.currency === currency &&
      !taken.has(account.id) &&
      (householdId ? account.householdId === householdId : account.isOwner)
    );
  });

  readonly selectedGoal = computed(() =>
    this.goals().find(goal => goal.id === this.selectedGoalId()) ?? null
  );

  // Cumulative contributions as an SVG polyline in a 100x40 box,
  // scaled so the top edge is the goal target.
  readonly chartPoints = computed(() => {
    const goal = this.selectedGoal();
    const items = [...this.contributions()].sort((a, b) =>
      a.contributionDateUtc.localeCompare(b.contributionDateUtc));

    if (!goal || items.length < 2) return '';

    const times = items.map(item => Date.parse(item.contributionDateUtc));
    const span = Math.max(1, times[times.length - 1] - times[0]);
    let total = 0;
    const totals = items.map(item => total += item.amount);
    const top = Math.max(goal.targetAmount, total);

    return items.map((_, index) => {
      const x = (times[index] - times[0]) / span * 100;
      const y = 40 - totals[index] / top * 40;
      return `${x.toFixed(2)},${y.toFixed(2)}`;
    }).join(' ');
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');

    forkJoin({
      goals: this.goalsService.getAll(),
      accounts: this.accountsService.getAccounts(),
      households: this.householdsService.getAll()
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: result => {
          this.goals.set(result.goals);
          this.accounts.set(result.accounts);
          this.households.set(result.households);
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
    const settingsError = this.validateSettings(values);
    if (settingsError) {
      this.error.set(settingsError);
      return;
    }

    const id = this.editingId();
    const base = {
      name: values.name.trim(),
      targetAmount: values.targetAmount,
      targetDateUtc: values.targetDate ? toUtcDate(values.targetDate, 0) : null,
      ...this.toSettings(values)
    };
    this.saving.set(true);
    this.error.set('');
    this.message.set('');

    const request = id
      ? this.goalsService.update(id, base)
      : this.goalsService.create({
          ...base,
          currency: values.currency,
          householdId: values.householdId || null,
          goalType: values.goalType
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
      householdId: goal.householdId ?? '',
      goalType: goal.goalType,
      currency: goal.currency,
      emergencyFundMonths: goal.emergencyFundMonths ?? 6,
      // For emergency funds the current computed target becomes the
      // fallback used if spending history later disappears.
      targetAmount: goal.targetAmount,
      targetDate: goal.targetDateUtc?.slice(0, 10) ?? '',
      tracking: goal.linkedAccountId ? 'linked' : 'manual',
      linkedAccountId: goal.linkedAccountId ?? '',
      autoEnabled: goal.autoContributionAmount !== null,
      autoAmount: goal.autoContributionAmount ?? 0,
      autoFrequency: goal.autoContributionFrequency ?? RecurrenceFrequency.Monthly,
      autoStart: goal.autoContributionStartUtc?.slice(0, 10) ?? today(),
      notificationsEnabled: goal.notificationsEnabled
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.goalForm.reset({
      name: '',
      householdId: '',
      goalType: SavingsGoalType.Custom,
      currency: this.currencies()[0] ?? '',
      emergencyFundMonths: 6,
      targetAmount: 0,
      targetDate: '',
      tracking: 'manual',
      linkedAccountId: '',
      autoEnabled: false,
      autoAmount: 0,
      autoFrequency: RecurrenceFrequency.Monthly,
      autoStart: today(),
      notificationsEnabled: true
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
    this.saving.set(true);
    this.error.set('');

    this.goalsService.addContribution(goal.id, {
      amount: value.amount,
      contributionDateUtc: toUtcDate(value.date, 12),
      note: value.note.trim()
    })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: updated => {
          this.replaceGoal(updated);
          this.contributionForm.reset({
            amount: 0,
            date: today(),
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

  statusLabel(status: SavingsGoalStatus): string {
    return STATUS_LABELS[status] ?? '';
  }

  frequencyLabel(frequency: RecurrenceFrequency | null): string {
    return frequency ? FREQUENCY_LABELS[frequency] : '';
  }

  householdName(id: string | null): string {
    return this.households().find(household => household.id === id)?.name
      ?? 'Household';
  }

  // Per-member totals for shared goals, largest first.
  memberTotals(): { name: string; amount: number }[] {
    const totals = new Map<string, number>();
    for (const item of this.contributions()) {
      const name = item.contributorEmail ?? 'Former member';
      totals.set(name, (totals.get(name) ?? 0) + item.amount);
    }
    return [...totals]
      .map(([name, amount]) => ({ name, amount }))
      .sort((a, b) => b.amount - a.amount);
  }

  private validateSettings(values: GoalFormValue): string {
    if (values.goalType === SavingsGoalType.EmergencyFund &&
        !(values.emergencyFundMonths >= 1 && values.emergencyFundMonths <= 24)) {
      return 'Choose between 1 and 24 months of expenses.';
    }
    if (values.tracking === 'linked' && !values.linkedAccountId) {
      return 'Choose an account to link.';
    }
    if (values.tracking === 'manual' && values.autoEnabled && !(values.autoAmount > 0)) {
      return 'Enter a scheduled contribution amount.';
    }
    return '';
  }

  private toSettings(values: GoalFormValue): SavingsGoalSettings {
    const linked = values.tracking === 'linked';
    const scheduled = !linked && values.autoEnabled;

    return {
      emergencyFundMonths: values.goalType === SavingsGoalType.EmergencyFund
        ? values.emergencyFundMonths
        : null,
      linkedAccountId: linked ? values.linkedAccountId : null,
      autoContributionAmount: scheduled ? values.autoAmount : null,
      autoContributionFrequency: scheduled ? Number(values.autoFrequency) : null,
      autoContributionStartUtc: scheduled ? toUtcDate(values.autoStart, 12) : null,
      notificationsEnabled: values.notificationsEnabled
    };
  }

  private replaceGoal(updated: SavingsGoal): void {
    this.goals.update(items =>
      items.map(item => item.id === updated.id ? updated : item)
    );
  }

  private getError(error: HttpErrorResponse, fallback: string): string {
    if (error.status === 401)
      return 'Your session has expired. Please sign in again.';

    if (error.status === 403)
      return 'Your household role does not allow changing this goal.';

    if (typeof error.error?.message === 'string')
      return error.error.message;

    return fallback;
  }
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}

function toUtcDate(date: string, hour: number): string {
  return new Date(`${date}T${String(hour).padStart(2, '0')}:00:00.000Z`)
    .toISOString();
}
