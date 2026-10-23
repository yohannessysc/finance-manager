
import {
  Component,
  computed,
  inject,
  OnInit,
  signal
} from '@angular/core';

import {
  CurrencyPipe,
  DatePipe
} from '@angular/common';

import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';

import { forkJoin, finalize } from 'rxjs';

import { Accounts } from '../accounts/accounts';
import { FinancialAccount } from '../accounts/financial-account.model';
import { RecentTransaction } from '../dashboard/dashboard.model';

import {
  SaveTransactionRequest,
  TransactionCategory,
  TransactionsService,
  TransactionType
} from './transactions.service';

@Component({
  selector: 'app-transactions',
  standalone: true,
  imports: [
    CurrencyPipe,
    DatePipe,
    ReactiveFormsModule
  ],
  templateUrl: './transactions.html',
  styleUrl: './transactions.scss'
})
export class Transactions implements OnInit {
  private readonly service = inject(TransactionsService);
  private readonly accountsService = inject(Accounts);
  private readonly fb = inject(FormBuilder);

  readonly types = TransactionType;
  readonly accounts = signal<FinancialAccount[]>([]);
  readonly categories = signal<TransactionCategory[]>([]);
  readonly transactions = signal<RecentTransaction[]>([]);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly deleting = signal(false);

  readonly error = signal('');
  readonly formError = signal('');
  readonly successMessage = signal('');

  readonly search = signal('');
  readonly typeFilter = signal('all');
  readonly accountFilter = signal('all');

  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly transactionToDelete = signal<RecentTransaction | null>(null);

  readonly transactionForm = this.fb.nonNullable.group({
    type: [TransactionType.Expense, Validators.required],
    financialAccountId: ['', Validators.required],
    destinationAccountId: [''],
    categoryId: [''],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    description: [
      '',
      [Validators.required, Validators.maxLength(500)]
    ],
    merchant: ['', Validators.maxLength(200)],
    transactionDate: ['', Validators.required]
  });

  readonly filteredCategories = computed(() => {
    const type = Number(this.transactionForm.controls.type.value);

    return this.categories()
      .filter(category => category.type === type)
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  readonly filteredTransactions = computed(() => {
    const search = this.search().trim().toLowerCase();
    const type = this.typeFilter();
    const account = this.accountFilter();

    return this.transactions()
      .filter(transaction => {
        if (
          type !== 'all' &&
          transaction.type !== Number(type)
        ) {
          return false;
        }

        if (
          account !== 'all' &&
          transaction.financialAccountId !== account &&
          transaction.destinationAccountId !== account
        ) {
          return false;
        }

        if (!search) return true;

        const category = this.categoryName(transaction);
        const source = this.accountName(
          transaction.financialAccountId
        );

        return [
          transaction.description,
          transaction.merchant ?? '',
          category,
          source
        ].some(value => value.toLowerCase().includes(search));
      })
      .sort((a, b) =>
        new Date(b.transactionDateUtc).getTime() -
        new Date(a.transactionDateUtc).getTime()
      );
  });

  readonly currencySummaries = computed(() => {
    const summaries = new Map<string, {
      income: number;
      expenses: number;
      count: number;
    }>();

    for (const transaction of this.filteredTransactions()) {
      const currency = this.currencyFor(transaction);
      const current = summaries.get(currency) ?? {
        income: 0,
        expenses: 0,
        count: 0
      };

      if (transaction.type === TransactionType.Income) {
        current.income += transaction.amount;
      }

      if (transaction.type === TransactionType.Expense) {
        current.expenses += transaction.amount;
      }

      current.count++;
      summaries.set(currency, current);
    }

    return [...summaries.entries()].map(([currency, value]) => ({
      currency,
      ...value,
      net: value.income - value.expenses
    }));
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set('');

    forkJoin({
      transactions: this.service.getTransactions(),
      categories: this.service.getCategories(),
      accounts: this.accountsService.getAccounts()
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: result => {
          this.transactions.set(result.transactions);
          this.categories.set(result.categories);
          this.accounts.set(result.accounts);
        },
        error: err => {
          console.error('Failed to load transactions:', err);
          this.error.set(
            'Unable to load transactions. Please try again.'
          );
        }
      });
  }

  accountName(id: string | null): string {
    if (!id) return '—';

    return this.accounts().find(
      account => account.id === id
    )?.name ?? 'Unknown account';
  }

  currencyFor(transaction: RecentTransaction): string {
    return this.accounts().find(
      account => account.id === transaction.financialAccountId
    )?.currency ?? 'CAD';
  }

  categoryName(transaction: RecentTransaction): string {
    if (transaction.type === TransactionType.Transfer) {
      return 'Transfer';
    }

    return this.categories().find(
      category => category.id === transaction.categoryId
    )?.name ?? 'Uncategorized';
  }

  typeLabel(type: number): string {
    switch (type) {
      case TransactionType.Income: return 'Income';
      case TransactionType.Expense: return 'Expense';
      case TransactionType.Transfer: return 'Transfer';
      default: return 'Unknown';
    }
  }

  onTypeChanged(): void {
    this.transactionForm.patchValue({
      categoryId: '',
      destinationAccountId: ''
    });
  }

  getDestinationAccounts(): FinancialAccount[] {
    const sourceId =
      this.transactionForm.controls.financialAccountId.value;

    const source = this.accounts().find(
      account => account.id === sourceId
    );

    return this.accounts().filter(account =>
      account.id !== sourceId &&
      (!source || account.currency === source.currency)
    );
  }

  onSourceChanged(): void {
    const destination =
      this.transactionForm.controls.destinationAccountId.value;

    if (
      destination &&
      !this.getDestinationAccounts().some(
        account => account.id === destination
      )
    ) {
      this.transactionForm.patchValue({
        destinationAccountId: ''
      });
    }
  }

  openCreate(type: TransactionType = TransactionType.Expense): void {
    this.editingId.set(null);
    this.formError.set('');
    this.successMessage.set('');

    this.transactionForm.reset({
      type,
      financialAccountId: this.accounts()[0]?.id ?? '',
      destinationAccountId: '',
      categoryId: '',
      amount: 0,
      description: '',
      merchant: '',
      transactionDate: this.localDateTimeNow()
    });

    this.showForm.set(true);
  }

  startEdit(transaction: RecentTransaction): void {
    if (transaction.recurringTransactionId) {
      this.error.set(
        'Automatically posted transactions cannot be edited here.'
      );
      return;
    }

    this.editingId.set(transaction.id);
    this.formError.set('');
    this.successMessage.set('');

    this.transactionForm.reset({
      type: transaction.type,
      financialAccountId: transaction.financialAccountId,
      destinationAccountId:
        transaction.destinationAccountId ?? '',
      categoryId: transaction.categoryId ?? '',
      amount: transaction.amount,
      description: transaction.description,
      merchant: transaction.merchant ?? '',
      transactionDate: this.toLocalDateTime(
        transaction.transactionDateUtc
      )
    });

    this.showForm.set(true);
  }

  closeForm(): void {
    if (this.saving()) return;

    this.showForm.set(false);
    this.editingId.set(null);
    this.formError.set('');
  }

  save(): void {
    if (this.saving()) return;

    if (this.transactionForm.invalid) {
      this.transactionForm.markAllAsTouched();
      return;
    }

    const value = this.transactionForm.getRawValue();
    const type = Number(value.type) as TransactionType;
    const source = this.accounts().find(
      account => account.id === value.financialAccountId
    );

    if (!source) {
      this.formError.set('Select a valid account.');
      return;
    }

    if (!Number.isFinite(value.amount) || value.amount <= 0) {
      this.formError.set('Enter an amount greater than zero.');
      return;
    }

    if (!value.description.trim()) {
      this.formError.set('Enter a transaction description.');
      return;
    }

    if (type === TransactionType.Transfer) {
      const destination = this.accounts().find(
        account => account.id === value.destinationAccountId
      );

      if (!destination || destination.id === source.id) {
        this.formError.set('Choose a different destination account.');
        return;
      }

      if (destination.currency !== source.currency) {
        this.formError.set(
          'Transfers currently require matching currencies.'
        );
        return;
      }
    }

    const localDate = new Date(value.transactionDate);

    if (Number.isNaN(localDate.getTime())) {
      this.formError.set('Enter a valid transaction date.');
      return;
    }

    const request: SaveTransactionRequest = {
      type,
      financialAccountId: source.id,
      destinationAccountId:
        type === TransactionType.Transfer
          ? value.destinationAccountId
          : null,
      categoryId:
        type === TransactionType.Transfer
          ? null
          : value.categoryId || null,
      amount: value.amount,
      description: value.description.trim(),
      merchant:
        type === TransactionType.Transfer
          ? null
          : value.merchant.trim() || null,
      transactionDateUtc: localDate.toISOString()
    };

    const editingId = this.editingId();

    const operation = editingId
      ? this.service.update(editingId, request)
      : this.service.create(request);

    this.saving.set(true);
    this.formError.set('');

    operation
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.showForm.set(false);
          this.editingId.set(null);
          this.successMessage.set(
            editingId
              ? 'Transaction updated successfully.'
              : 'Transaction recorded successfully.'
          );
          this.loadData();
        },
        error: err => {
          console.error('Saving transaction failed:', err);
          this.formError.set(
            err.error?.message ??
            'Unable to save transaction. Please try again.'
          );
        }
      });
  }

  requestDelete(transaction: RecentTransaction): void {
    if (transaction.recurringTransactionId) {
      this.error.set(
        'Automatically posted transactions cannot be deleted here.'
      );
      return;
    }

    this.transactionToDelete.set(transaction);
  }

  cancelDelete(): void {
    if (!this.deleting()) {
      this.transactionToDelete.set(null);
    }
  }

  confirmDelete(): void {
    const transaction = this.transactionToDelete();

    if (!transaction || this.deleting()) return;

    this.deleting.set(true);
    this.error.set('');

    this.service.delete(transaction.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.transactionToDelete.set(null);
          this.successMessage.set('Transaction deleted.');
          this.loadData();
        },
        error: err => {
          console.error('Deleting transaction failed:', err);
          this.transactionToDelete.set(null);
          this.error.set(
            err.error?.message ??
            'Unable to delete transaction.'
          );
        }
      });
  }

  private localDateTimeNow(): string {
    const date = new Date();
    const offset = date.getTimezoneOffset() * 60000;

    return new Date(date.getTime() - offset)
      .toISOString()
      .slice(0, 16);
  }

  private toLocalDateTime(utcDate: string): string {
    const date = new Date(utcDate);
    const offset = date.getTimezoneOffset() * 60000;

    return new Date(date.getTime() - offset)
      .toISOString()
      .slice(0, 16);
  }
}
