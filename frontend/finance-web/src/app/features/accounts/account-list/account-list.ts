
import { CurrencyPipe } from '@angular/common';
import {
  Component,
  computed,
  inject,
  OnInit,
  signal
} from '@angular/core';

import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { RouterLink } from '@angular/router';

import { forkJoin, finalize } from 'rxjs';

import { Accounts } from '../accounts';
import { FinancialAccount } from '../financial-account.model';
import { AccountBalance } from '../../dashboard/dashboard.model';
import {
  Household,
  HouseholdsService
} from '../../households/households.service';

@Component({
  selector: 'app-account-list',
  standalone: true,
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink],
  templateUrl: './account-list.html',
  styleUrl: './account-list.scss'
})
export class AccountList implements OnInit {
  private readonly accountsService = inject(Accounts);
  private readonly fb = inject(FormBuilder);
  private readonly householdsService = inject(HouseholdsService);

  readonly accounts = signal<FinancialAccount[]>([]);
  readonly balances = signal<AccountBalance[]>([]);
  readonly households = signal<Household[]>([]);
  readonly shareHouseholdId = signal('');

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly deleting = signal(false);
  readonly sharingAccountId = signal<string | null>(null);

  readonly showForm = signal(false);
  readonly editingAccountId = signal<string | null>(null);
  readonly accountToDelete = signal<FinancialAccount | null>(null);

  readonly error = signal('');
  readonly formError = signal('');
  readonly successMessage = signal('');

  readonly accountTypes = [
    'Chequing',
    'Savings',
    'Cash',
    'CreditCard'
  ];

  readonly currencySummaries = computed(() => {
    const totals = new Map<string, {
      total: number;
      count: number;
    }>();

    for (const account of this.accounts()) {
      const current = totals.get(account.currency) ?? {
        total: 0,
        count: 0
      };

      const balance = this.balances().find(
        item => item.accountId === account.id
      );

      current.total += balance?.currentBalance
        ?? account.openingBalance;

      current.count++;

      totals.set(account.currency, current);
    }

    return [...totals.entries()].map(([currency, value]) => ({
      currency,
      total: value.total,
      count: value.count
    }));
  });

  readonly accountForm = this.fb.nonNullable.group({
    name: [
      '',
      [Validators.required, Validators.maxLength(150)]
    ],
    accountType: ['Chequing', Validators.required],
    openingBalance: [0, Validators.required]
  });

  ngOnInit(): void {
    this.loadAccounts();
  }

  loadAccounts(): void {
    this.loading.set(true);
    this.error.set('');

    forkJoin({
      accounts: this.accountsService.getAccounts(),
      balances: this.accountsService.getBalances(),
      households: this.householdsService.getAll()
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: ({ accounts, balances, households }) => {
          this.accounts.set(
            [...accounts].sort(
              (a, b) => a.name.localeCompare(b.name)
            )
          );

          this.balances.set(balances);
          this.households.set(households);
          if (
            !households.some(x => x.id === this.shareHouseholdId())
          ) {
            this.shareHouseholdId.set(households[0]?.id ?? '');
          }
        },
        error: err => {
          console.error('Loading accounts failed:', err);
          this.error.set(
            'Unable to load accounts or balances. Please try again.'
          );
        }
      });
  }

  getCurrentBalance(account: FinancialAccount): number {
    return this.balances().find(
      balance => balance.accountId === account.id
    )?.currentBalance ?? account.openingBalance;
  }

  getBalanceDetails(
    account: FinancialAccount
  ): AccountBalance | undefined {
    return this.balances().find(
      balance => balance.accountId === account.id
    );
  }

  getAccountTypeLabel(type: string): string {
    return type === 'CreditCard' ? 'Credit Card' : type;
  }

  householdName(id: string | null): string {
    if (!id) return '';
    return this.households().find(x => x.id === id)?.name ?? 'household';
  }

  shareAccount(account: FinancialAccount): void {
    const householdId = this.shareHouseholdId();
    if (!householdId || this.sharingAccountId()) return;

    this.sharingAccountId.set(account.id);
    this.error.set('');
    this.accountsService.shareAccount(account.id, householdId)
      .pipe(finalize(() => this.sharingAccountId.set(null)))
      .subscribe({
        next: () => {
          this.successMessage.set(
            `${account.name} is now shared with ${this.householdName(householdId)}.`
          );
          this.loadAccounts();
        },
        error: err => this.error.set(
          err.error?.message ?? 'Unable to share this account.'
        )
      });
  }

  unshareAccount(account: FinancialAccount): void {
    if (this.sharingAccountId()) return;
    this.sharingAccountId.set(account.id);
    this.error.set('');
    this.accountsService.unshareAccount(account.id)
      .pipe(finalize(() => this.sharingAccountId.set(null)))
      .subscribe({
        next: () => {
          this.successMessage.set(`${account.name} is private again.`);
          this.loadAccounts();
        },
        error: err => this.error.set(
          err.error?.message ?? 'Unable to stop sharing this account.'
        )
      });
  }

  openCreateForm(): void {
    this.editingAccountId.set(null);
    this.accountForm.reset({
      name: '',
      accountType: 'Chequing',
      openingBalance: 0
    });

    this.formError.set('');
    this.successMessage.set('');
    this.showForm.set(true);
  }

  startEdit(account: FinancialAccount): void {
    this.editingAccountId.set(account.id);

    this.accountForm.setValue({
      name: account.name,
      accountType: account.accountType,
      openingBalance: account.openingBalance
    });

    this.formError.set('');
    this.successMessage.set('');
    this.showForm.set(true);
  }

  cancelForm(): void {
    if (this.saving()) return;

    this.showForm.set(false);
    this.editingAccountId.set(null);
    this.formError.set('');
  }

  saveAccount(): void {
    if (this.saving()) return;

    if (this.accountForm.invalid) {
      this.accountForm.markAllAsTouched();
      return;
    }

    const request = this.accountForm.getRawValue();

    if (!Number.isFinite(request.openingBalance)) {
      this.formError.set('Enter a valid opening balance.');
      return;
    }

    const editingId = this.editingAccountId();

    const operation = editingId
      ? this.accountsService.updateAccount(
          editingId,
          request
        )
      : this.accountsService.createAccount(request);

    this.saving.set(true);
    this.formError.set('');

    operation
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.showForm.set(false);
          this.editingAccountId.set(null);

          this.successMessage.set(
            editingId
              ? 'Account updated successfully.'
              : 'Account created successfully.'
          );

          this.loadAccounts();
        },
        error: err => {
          console.error('Saving account failed:', err);

          this.formError.set(
            err.error?.message ||
            'Unable to save account. Please try again.'
          );
        }
      });
  }

  requestDelete(account: FinancialAccount): void {
    this.error.set('');
    this.accountToDelete.set(account);
  }

  cancelDelete(): void {
    if (this.deleting()) return;
    this.accountToDelete.set(null);
  }

  confirmDelete(): void {
    const account = this.accountToDelete();

    if (!account || this.deleting()) return;

    this.deleting.set(true);
    this.error.set('');

    this.accountsService.deleteAccount(account.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.accountToDelete.set(null);
          this.successMessage.set(
            'Account deleted successfully.'
          );
          this.loadAccounts();
        },
        error: err => {
          console.error('Deleting account failed:', err);
          this.accountToDelete.set(null);
          this.error.set(
            'Unable to delete this account. It may have transactions or recurring payments linked to it.'
          );
        }
      });
  }
}
