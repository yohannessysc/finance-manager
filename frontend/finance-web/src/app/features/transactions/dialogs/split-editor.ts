import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, input, OnInit, output, signal } from '@angular/core';

import { categoryOptions } from '../../../core/services/categories.service';
import { RecentTransaction } from '../../dashboard/dashboard.model';
import { TransactionCategory, TransactionsService } from '../transactions.service';

interface SplitRow {
  categoryId: string;
  amount: number | null;
  note: string;
}

// Splits one transaction across several categories (e.g. a Costco
// receipt between Groceries and Household). The lines must add up to
// the transaction amount.
@Component({
  selector: 'app-split-editor',
  standalone: true,
  imports: [CurrencyPipe],
  styleUrl: './dialog.scss',
  template: `
    <div class="modal-backdrop">
      <section class="modal split-modal" role="dialog" aria-modal="true" aria-labelledby="split-title">
        <div class="modal-header">
          <div>
            <h2 id="split-title">Split transaction</h2>
            <p>{{ transaction().description }} ·
              {{ transaction().amount | currency: currency() : 'symbol' : '1.2-2' }}</p>
          </div>
          <button type="button" class="close-button" aria-label="Close" [disabled]="saving()" (click)="closed.emit()">×</button>
        </div>

        <div class="split-rows">
          @for (row of rows(); track $index; let i = $index) {
            <div class="split-row">
              <div class="form-field">
                <label [for]="'split-category-' + i">Category</label>
                <select [id]="'split-category-' + i" [value]="row.categoryId"
                        (change)="update(i, { categoryId: $any($event.target).value })">
                  <option value="">Choose…</option>
                  @for (option of options(); track option.id) {
                    <option [value]="option.id" [selected]="option.id === row.categoryId">{{ option.label }}</option>
                  }
                </select>
              </div>
              <div class="form-field amount-field">
                <label [for]="'split-amount-' + i">Amount</label>
                <input [id]="'split-amount-' + i" type="number" min="0.01" step="0.01" inputmode="decimal"
                       [value]="row.amount ?? ''"
                       (input)="update(i, { amount: toAmount($any($event.target).value) })" />
              </div>
              <div class="form-field note-field">
                <label [for]="'split-note-' + i">Note</label>
                <input [id]="'split-note-' + i" maxlength="200" placeholder="Optional" [value]="row.note"
                       (input)="update(i, { note: $any($event.target).value })" />
              </div>
              <button type="button" class="text-button danger remove" [disabled]="rows().length <= 2"
                      [attr.aria-label]="'Remove line ' + (i + 1)" (click)="remove(i)">✕</button>
            </div>
          }
        </div>

        <div class="split-footer">
          <button type="button" class="text-button" [disabled]="rows().length >= 20" (click)="add()">+ Add line</button>
          <span class="remaining" [class.off]="remaining() !== 0">
            @if (remaining() === 0) {
              ✓ Adds up
            } @else if (remaining() > 0) {
              {{ remaining() | currency: currency() : 'symbol' : '1.2-2' }} left to assign
            } @else {
              {{ -remaining() | currency: currency() : 'symbol' : '1.2-2' }} over
            }
          </span>
        </div>

        @if (error()) {
          <p class="form-error" role="alert">{{ error() }}</p>
        }

        <div class="modal-actions">
          @if (isSplit()) {
            <button type="button" class="text-button danger" [disabled]="saving()" (click)="save(true)">Remove split</button>
          }
          <span class="spacer"></span>
          <button type="button" class="btn secondary" [disabled]="saving()" (click)="closed.emit()">Cancel</button>
          <button type="button" class="btn primary" [disabled]="!canSave()" (click)="save(false)">
            {{ saving() ? 'Saving…' : 'Save split' }}
          </button>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .split-modal { max-width: 680px; }
    .split-row {
      display: grid;
      grid-template-columns: minmax(0, 1.4fr) minmax(0, .8fr) minmax(0, 1fr) 28px;
      gap: 10px;
      align-items: end;
    }
    .split-row .form-field { margin-bottom: 12px; }
    .remove { margin-bottom: 18px; }
    .split-footer { display: flex; justify-content: space-between; align-items: center; }
    .remaining { font-size: 13px; font-weight: 700; color: var(--fm-primary); }
    .remaining.off { color: #c2410c; }
    .spacer { flex: 1; }
    @media (max-width: 600px) {
      .split-row { grid-template-columns: 1fr 1fr 28px; }
      .note-field { grid-column: 1 / 3; grid-row: 2; }
    }
  `]
})
export class SplitEditor implements OnInit {
  private readonly service = inject(TransactionsService);

  readonly transaction = input.required<RecentTransaction>();
  readonly categories = input.required<TransactionCategory[]>();
  readonly currency = input('CAD');

  readonly saved = output<RecentTransaction>();
  readonly closed = output<void>();

  readonly rows = signal<SplitRow[]>([]);
  readonly saving = signal(false);
  readonly error = signal('');

  readonly options = computed(() =>
    categoryOptions(this.categories().filter(c => c.type === this.transaction().type)));

  readonly isSplit = computed(() => (this.transaction().splits?.length ?? 0) > 0);

  // In cents to avoid floating point drift.
  readonly remaining = computed(() => {
    const total = Math.round(this.transaction().amount * 100);
    const assigned = this.rows().reduce((sum, row) => sum + Math.round((row.amount ?? 0) * 100), 0);
    return (total - assigned) / 100;
  });

  readonly canSave = computed(() =>
    !this.saving() &&
    this.remaining() === 0 &&
    this.rows().length >= 2 &&
    this.rows().every(row => row.categoryId && (row.amount ?? 0) > 0));

  ngOnInit(): void {
    const transaction = this.transaction();
    const existing = transaction.splits ?? [];
    this.rows.set(existing.length > 0
      ? existing.map(line => ({ categoryId: line.categoryId, amount: line.amount, note: line.note ?? '' }))
      : [
          { categoryId: transaction.categoryId ?? '', amount: transaction.amount, note: '' },
          { categoryId: '', amount: null, note: '' }
        ]);
  }

  toAmount(value: string): number | null {
    const parsed = Number.parseFloat(value);
    return Number.isFinite(parsed) ? Math.round(parsed * 100) / 100 : null;
  }

  update(index: number, change: Partial<SplitRow>): void {
    this.rows.update(rows => rows.map((row, i) => (i === index ? { ...row, ...change } : row)));
  }

  add(): void {
    // Start the new line with whatever is still unassigned.
    const remaining = this.remaining();
    this.rows.update(rows => [...rows, { categoryId: '', amount: remaining > 0 ? remaining : null, note: '' }]);
  }

  remove(index: number): void {
    this.rows.update(rows => rows.filter((_, i) => i !== index));
  }

  save(removeSplit: boolean): void {
    if (this.saving() || (!removeSplit && !this.canSave())) return;

    this.saving.set(true);
    this.error.set('');

    const lines = removeSplit
      ? []
      : this.rows().map(row => ({
          categoryId: row.categoryId,
          amount: row.amount ?? 0,
          note: row.note.trim() || null
        }));

    this.service.setSplits(this.transaction().id, lines).subscribe({
      next: transaction => {
        this.saving.set(false);
        this.saved.emit(transaction);
      },
      error: (err: HttpErrorResponse) => {
        this.saving.set(false);
        this.error.set(err.error?.message ?? 'Could not save the split. Please try again.');
      }
    });
  }
}
