
import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { Router } from '@angular/router';

import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';

import { finalize } from 'rxjs';

import { Accounts } from '../accounts';
import { FinancialAccount } from '../financial-account.model';
import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-account-list',
  standalone: true,
  imports: [CurrencyPipe, ReactiveFormsModule],
  templateUrl: './account-list.html',
  styleUrl: './account-list.scss'
})
export class AccountList implements OnInit {

  private readonly accountsService = inject(Accounts);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  // ---------------------------------------
  // Account Data
  // ---------------------------------------

  readonly accounts = signal<FinancialAccount[]>([]);

  // ---------------------------------------
  // UI States
  // ---------------------------------------

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly showForm = signal(false);
  readonly loggingOut = signal(false);

  // ---------------------------------------
  // Edit / Delete States
  // ---------------------------------------

  readonly editingAccountId = signal<string | null>(null);
  readonly deletingAccountId = signal<string | null>(null);

  // ---------------------------------------
  // Messages
  // ---------------------------------------

  readonly error = signal('');
  readonly formError = signal('');
  readonly successMessage = signal('');

  // ---------------------------------------
  // Supported Account Types
  // ---------------------------------------

  readonly accountTypes = [
    'Chequing',
    'Savings',
    'Cash',
    'CreditCard'
  ];

  // ---------------------------------------
  // Account Form
  // ---------------------------------------

  readonly accountForm = this.fb.nonNullable.group({
    name: [
      '',
      [
        Validators.required,
        Validators.maxLength(150)
      ]
    ],
    accountType: [
      'Chequing',
      Validators.required
    ],
    openingBalance: [
      0,
      Validators.required
    ]
  });

  // ---------------------------------------
  // Initialize
  // ---------------------------------------

  ngOnInit(): void {
    this.loadAccounts();
  }

  // ---------------------------------------
  // GET - Load Accounts
  // ---------------------------------------

  loadAccounts(): void {
    this.loading.set(true);
    this.error.set('');

    this.accountsService.getAccounts()
      .pipe(
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (data) => {
          this.accounts.set(data);
        },
        error: (err) => {
          console.error('Failed to load accounts:', err);
          this.error.set(
            'Unable to load financial accounts.'
          );
        }
      });
  }

  // ---------------------------------------
  // Show / Hide Account Form
  // ---------------------------------------

  toggleForm(): void {
    if (this.saving()) return;

    if (this.showForm()) {
      this.cancelForm();
      return;
    }

    this.resetForm();
    this.showForm.set(true);
    this.successMessage.set('');
  }

  // ---------------------------------------
  // Start Editing an Account
  // ---------------------------------------

  startEdit(account: FinancialAccount): void {
    if (this.saving() || this.deletingAccountId()) {
      return;
    }

    this.formError.set('');
    this.successMessage.set('');

    this.editingAccountId.set(account.id);

    this.accountForm.setValue({
      name: account.name,
      accountType: account.accountType,
      openingBalance: account.openingBalance
    });

    this.showForm.set(true);
  }

  // ---------------------------------------
  // Cancel Create / Edit
  // ---------------------------------------

  cancelForm(): void {
    if (this.saving()) return;

    this.resetForm();
    this.showForm.set(false);
    this.formError.set('');
  }

  // ---------------------------------------
  // Reset Form
  // ---------------------------------------

  private resetForm(): void {
    this.editingAccountId.set(null);

    this.accountForm.reset({
      name: '',
      accountType: 'Chequing',
      openingBalance: 0
    });
  }

  // ---------------------------------------
  // POST / PUT - Save Account
  // ---------------------------------------

  saveAccount(): void {
    if (this.saving()) return;

    if (this.accountForm.invalid) {
      this.accountForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.formError.set('');
    this.successMessage.set('');

    const request = this.accountForm.getRawValue();
    const editingId = this.editingAccountId();

    const operation = editingId
      ? this.accountsService.updateAccount(editingId, request)
      : this.accountsService.createAccount(request);

    operation
      .pipe(
        finalize(() => this.saving.set(false))
      )
      .subscribe({
        next: (savedAccount) => {
          this.accounts.update(current => {
            const updatedAccounts = editingId
              ? current.map(account =>
                  account.id === editingId
                    ? savedAccount
                    : account
                )
              : [...current, savedAccount];

            return updatedAccounts.sort(
              (a, b) => a.name.localeCompare(b.name)
            );
          });

          this.resetForm();
          this.showForm.set(false);

          this.successMessage.set(
            editingId
              ? 'Account updated successfully.'
              : 'Account created successfully.'
          );
        },
        error: (err) => {
          console.error('Failed to save account:', err);

          this.formError.set(
            err.error?.message ||
            'Unable to save financial account.'
          );
        }
      });
  }

  // ---------------------------------------
  // DELETE - Remove Account
  // ---------------------------------------

  deleteAccount(account: FinancialAccount): void {
    if (this.deletingAccountId() || this.saving()) {
      return;
    }

    const confirmed = window.confirm(
      `Are you sure you want to permanently delete "${account.name}"?`
    );

    if (!confirmed) return;

    this.deletingAccountId.set(account.id);
    this.error.set('');
    this.successMessage.set('');

    this.accountsService.deleteAccount(account.id)
      .pipe(
        finalize(() => this.deletingAccountId.set(null))
      )
      .subscribe({
        next: () => {
          this.accounts.update(current =>
            current.filter(item => item.id !== account.id)
          );

          if (this.editingAccountId() === account.id) {
            this.resetForm();
            this.showForm.set(false);
          }

          this.successMessage.set(
            'Account deleted successfully.'
          );
        },
        error: (err) => {
          console.error('Failed to delete account:', err);

          this.error.set(
            'Unable to delete financial account.'
          );
        }
      });
  }

  // ---------------------------------------
  // LOGOUT - End Authenticated Session
  // ---------------------------------------

  logout(): void {
    if (this.loggingOut()) return;

    this.loggingOut.set(true);
    this.error.set('');

    this.authService.logout()
      .pipe(
        finalize(() => this.loggingOut.set(false))
      )
      .subscribe({
        next: () => {
          this.accounts.set([]);
          this.resetForm();
          this.showForm.set(false);

          this.router.navigateByUrl('/login', {
            replaceUrl: true
          });
        },
        error: (err) => {
          console.error('Logout failed:', err);

          this.error.set(
            'Unable to log out. Please try again.'
          );
        }
      });
  }
}
