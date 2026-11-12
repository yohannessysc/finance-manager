import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, output, signal } from '@angular/core';

import { FinancialAccount } from '../../accounts/financial-account.model';
import { CsvImportResult, TransactionsService } from '../transactions.service';

type FileKind = 'csv' | 'ofx' | null;

// Imports a statement downloaded from online banking: OFX/QFX into one
// chosen account, or a CSV whose rows name their accounts.
@Component({
  selector: 'app-import-dialog',
  standalone: true,
  styleUrl: './dialog.scss',
  template: `
    <div class="modal-backdrop">
      <section class="modal" role="dialog" aria-modal="true" aria-labelledby="import-title">
        <div class="modal-header">
          <div>
            <h2 id="import-title">Import transactions</h2>
            <p>Upload a statement downloaded from your online banking.</p>
          </div>
          <button type="button" class="close-button" aria-label="Close" [disabled]="importing()" (click)="close()">×</button>
        </div>

        @if (result(); as summary) {
          <div class="result" role="status">
            <strong>{{ summary.imported }} imported</strong>
            @if (summary.duplicates > 0) {
              <span>{{ summary.duplicates }} already in your account, skipped</span>
            }
            @if (summary.invalidRows > 0) {
              <span class="warn">{{ summary.invalidRows }} could not be imported</span>
            }
          </div>
          @if (summary.errors.length > 0) {
            <ul class="errors">
              @for (problem of summary.errors; track $index) {
                <li>{{ kind() === 'csv' ? 'Row ' + problem.row + ': ' : '' }}{{ problem.message }}</li>
              }
            </ul>
          }
          <div class="modal-actions">
            <button type="button" class="btn secondary" (click)="reset()">Import another</button>
            <button type="button" class="btn primary" (click)="close()">Done</button>
          </div>
        } @else {
          <div class="form-field">
            <label for="import-file">Statement file</label>
            <input id="import-file" type="file" accept=".ofx,.qfx,.csv" (change)="choose($event)" />
            <small>OFX or QFX (often called "Money", "Quicken" or "OFX" in your bank's download options) or CSV. Up to 5 MB.</small>
          </div>

          @if (kind() === 'ofx') {
            <div class="form-field">
              <label for="import-account">Import into</label>
              <select id="import-account" [value]="accountId()" (change)="accountId.set($any($event.target).value)">
                <option value="">Choose an account…</option>
                @for (account of importableAccounts(); track account.id) {
                  <option [value]="account.id">{{ account.name }} · {{ account.currency }}</option>
                }
              </select>
              @if (importableAccounts().length === 0) {
                <small>Add an account first. Accounts that sync from your bank don't need statement imports.</small>
              } @else {
                <small>Transactions already imported from an earlier statement are skipped.</small>
              }
            </div>
          }

          @if (kind() === 'csv') {
            <p class="muted">
              The CSV needs the columns <strong>date, description, amount, account</strong> (the account's name in this app).
              Negative amounts are expenses. Optional: currency, type, category, merchant.
              <a href="/api/v1/transactions/import/csv/template" download>Download a template</a>.
            </p>
          }

          @if (error()) {
            <p class="form-error" role="alert">{{ error() }}</p>
          }

          <div class="modal-actions">
            <button type="button" class="btn secondary" [disabled]="importing()" (click)="close()">Cancel</button>
            <button type="button" class="btn primary" [disabled]="!canImport()" (click)="import()">
              {{ importing() ? 'Importing…' : 'Import' }}
            </button>
          </div>
        }
      </section>
    </div>
  `,
  styles: [`
    .result {
      display: flex;
      flex-direction: column;
      gap: 6px;
      padding: 16px;
      border-radius: 12px;
      background: var(--fm-hover);
      font-size: 14px;
    }
    .result .warn { color: #c2410c; }
    .errors {
      max-height: 180px;
      overflow-y: auto;
      margin: 12px 0 0;
      padding-left: 18px;
      color: var(--fm-muted);
      font-size: 12px;
    }
    .muted a { color: var(--fm-primary); }
  `]
})
export class ImportDialog {
  private readonly service = inject(TransactionsService);

  readonly accounts = input.required<FinancialAccount[]>();

  // Emitted when the dialog closes; true if anything was imported.
  readonly closed = output<boolean>();

  readonly file = signal<File | null>(null);
  readonly accountId = signal('');
  readonly importing = signal(false);
  readonly error = signal('');
  readonly result = signal<CsvImportResult | null>(null);
  private importedAny = false;

  readonly kind = computed<FileKind>(() => {
    const name = this.file()?.name.toLowerCase() ?? '';
    if (name.endsWith('.csv')) return 'csv';
    if (name.endsWith('.ofx') || name.endsWith('.qfx')) return 'ofx';
    return null;
  });

  readonly importableAccounts = computed(() =>
    this.accounts().filter(account => !account.isBankLinked));

  readonly canImport = computed(() =>
    !this.importing() &&
    this.kind() !== null &&
    (this.kind() === 'csv' || !!this.accountId()));

  choose(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.file.set(file);
    this.error.set(file && !this.kind() ? 'Choose a .ofx, .qfx or .csv file.' : '');
    if (this.importableAccounts().length === 1) {
      this.accountId.set(this.importableAccounts()[0].id);
    }
  }

  import(): void {
    const file = this.file();
    if (!file || !this.canImport()) return;

    this.importing.set(true);
    this.error.set('');

    const request$ = this.kind() === 'csv'
      ? this.service.importCsv(file)
      : this.service.importOfx(file, this.accountId());

    request$.subscribe({
      next: result => {
        this.importing.set(false);
        this.importedAny ||= result.imported > 0;
        this.result.set(result);
      },
      error: (err: HttpErrorResponse) => {
        this.importing.set(false);
        this.error.set(err.error?.message ?? 'The import failed. Check the file and try again.');
      }
    });
  }

  reset(): void {
    this.file.set(null);
    this.result.set(null);
    this.error.set('');
  }

  close(): void {
    if (this.importing()) return;
    this.closed.emit(this.importedAny);
  }
}
