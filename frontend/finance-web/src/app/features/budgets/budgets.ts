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
import { CategoriesService, Category, categoryOptions } from '../../core/services/categories.service';
import { BudgetsService, Budget, BudgetUpsertRequest } from './budgets.service';
import {
  Household,
  HouseholdsService
} from '../households/households.service';

@Component({
  selector: 'app-budgets',
  standalone: true,
  imports: [ReactiveFormsModule, CurrencyPipe, DatePipe, DecimalPipe],
  templateUrl: './budgets.html',
  styleUrl: './budgets.scss'
})
export class Budgets implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly budgetsService = inject(BudgetsService);
  private readonly categoriesService = inject(CategoriesService);
  private readonly accountsService = inject(Accounts);
  private readonly householdsService = inject(HouseholdsService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly budgets = signal<Budget[]>([]);
  readonly expenseCategories = signal<Category[]>([]);
  readonly currencies = signal<string[]>([]);
  readonly households = signal<Household[]>([]);
  readonly editingId = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    categoryId: ['', Validators.required],
    currency: ['', Validators.required],
    monthlyLimit: [
      0,
      [
        Validators.required,
        Validators.min(0.01),
        Validators.max(9999999999999999.99)
      ]
    ],
    householdId: ['']
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');

    forkJoin({
      budgets: this.budgetsService.getAll(),
      categories: this.categoriesService.getAll(),
      accounts: this.accountsService.getAccounts(),
      households: this.householdsService.getAll()
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: result => {
          this.budgets.set(result.budgets);
          this.households.set(result.households);
          // Subcategories listed under their parent as "Parent › Child".
          this.expenseCategories.set(
            categoryOptions(result.categories.filter(category => category.type === 2))
              .map(option => ({ ...option.category, name: option.label }))
          );
          const currencies = [
            ...new Set(result.accounts.map(account => account.currency))
          ].sort();
          this.currencies.set(currencies);

          if (!this.form.controls.currency.value && currencies.length) {
            this.form.controls.currency.setValue(currencies[0]);
          }
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(
            this.getError(error, 'Unable to load your budgets.')
          );
        }
      });
  }

  save(): void {
    if (this.saving()) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.message.set('');

    const values = this.form.getRawValue();
    const request: BudgetUpsertRequest = {
      ...values,
      householdId: values.householdId || null
    };
    const id = this.editingId();
    const saveRequest = id
      ? this.budgetsService.update(id, request)
      : this.budgetsService.create(request);

    saveRequest
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: budget => {
          this.budgets.update(items =>
            id
              ? items.map(item => item.id === id ? budget : item)
              : [...items, budget]
          );
          this.budgets.update(items =>
            [...items].sort((a, b) =>
              a.categoryName.localeCompare(b.categoryName) ||
              a.currency.localeCompare(b.currency)
            )
          );
          this.message.set(id ? 'Budget updated.' : 'Budget created.');
          this.cancelEdit();
        },
        error: (error: HttpErrorResponse) => {
          this.error.set(this.getError(error, 'Unable to save this budget.'));
        }
      });
  }

  edit(budget: Budget): void {
    this.editingId.set(budget.id);
    this.error.set('');
    this.message.set('');
    if (budget.householdId) {
      this.form.controls.householdId.disable();
    } else {
      this.form.controls.householdId.enable();
    }
    this.form.setValue({
      categoryId: budget.categoryId,
      currency: budget.currency,
      monthlyLimit: budget.monthlyLimit,
      householdId: budget.householdId ?? ''
    });
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.form.controls.householdId.enable();
    this.form.reset({
      categoryId: '',
      currency: this.currencies()[0] ?? '',
      monthlyLimit: 0,
      householdId: ''
    });
  }

  remove(budget: Budget): void {
    this.error.set('');
    this.message.set('');

    this.budgetsService.delete(budget.id).subscribe({
      next: () => {
        this.budgets.update(items =>
          items.filter(item => item.id !== budget.id)
        );
        this.message.set(`${budget.categoryName} budget deleted.`);
        if (this.editingId() === budget.id) this.cancelEdit();
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(this.getError(error, 'Unable to delete this budget.'));
      }
    });
  }

  barWidth(budget: Budget): number {
    return Math.max(0, Math.min(100, budget.percentSpent));
  }

  householdName(id: string | null): string {
    if (!id) return 'Personal';
    return this.households().find(x => x.id === id)?.name ?? 'Household';
  }

  private getError(error: HttpErrorResponse, fallback: string): string {
    if (error.status === 401)
      return 'Your session has expired. Please sign in again.';

    if (typeof error.error?.message === 'string')
      return error.error.message;

    return fallback;
  }
}
