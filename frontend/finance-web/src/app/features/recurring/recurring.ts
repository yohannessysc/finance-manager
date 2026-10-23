
import { CurrencyPipe, DatePipe } from '@angular/common';
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
import { forkJoin, finalize } from 'rxjs';

import { Accounts } from '../accounts/accounts';
import { FinancialAccount } from '../accounts/financial-account.model';
import {
  TransactionsService,
  TransactionCategory,
  TransactionType
} from '../transactions/transactions.service';
import {
  RecurringService,
  RecurringTransaction,
  RecurrenceFrequency,
  RecurringStatus,
  SaveRecurringRequest
} from './recurring.service';

@Component({
  selector: 'app-recurring',
  standalone: true,
  imports: [CurrencyPipe, DatePipe, ReactiveFormsModule],
  templateUrl: './recurring.html',
  styleUrl: './recurring.scss'
})
export class Recurring implements OnInit {
  private readonly service = inject(RecurringService);
  private readonly accountsService = inject(Accounts);
  private readonly transactionsService = inject(TransactionsService);
  private readonly fb = inject(FormBuilder);

  readonly types = TransactionType;
  readonly frequencies = RecurrenceFrequency;
  readonly statuses = RecurringStatus;

  readonly schedules = signal<RecurringTransaction[]>([]);
  readonly accounts = signal<FinancialAccount[]>([]);
  readonly categories = signal<TransactionCategory[]>([]);

  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly workingId = signal<string | null>(null);
  readonly showForm = signal(false);
  readonly editingId = signal<string | null>(null);

  readonly error = signal('');
  readonly formError = signal('');
  readonly notice = signal('');

  readonly statusFilter = signal('all');
  readonly typeFilter = signal('all');
  readonly search = signal('');
  readonly confirmation = signal<{
    id: string;
    description: string;
    action: 'pause' | 'resume' | 'cancel';
  } | null>(null);

  readonly timeZones = [
    'America/Toronto',
    'America/Vancouver',
    'America/Edmonton',
    'America/Winnipeg',
    'America/Halifax',
    'America/St_Johns',
    'Etc/UTC'
  ];

  readonly form = this.fb.nonNullable.group({
    type: [TransactionType.Expense, Validators.required],
    financialAccountId: ['', Validators.required],
    destinationAccountId: [''],
    categoryId: [''],
    amount: [0, [Validators.required, Validators.min(0.01)]],
    description: [
      '',
      [Validators.required, Validators.maxLength(500)]
    ],
    frequency: [RecurrenceFrequency.Monthly, Validators.required],
    startDate: ['', Validators.required],
    endDate: [''],
    timeZoneId: ['America/Toronto', Validators.required]
  });

  readonly filtered = computed(() => {
    const status = this.statusFilter();
    const type = this.typeFilter();
    const query = this.search().trim().toLowerCase();

    return this.schedules()
      .filter(item =>
        status === 'all' || item.status === Number(status)
      )
      .filter(item =>
        type === 'all' || item.type === Number(type)
      )
      .filter(item => {
        if (!query) return true;
        return [
          item.description,
          this.accountName(item.financialAccountId),
          this.categoryName(item.categoryId)
        ].some(value => value.toLowerCase().includes(query));
      })
      .sort((a, b) =>
        new Date(a.nextOccurrenceUtc).getTime() -
        new Date(b.nextOccurrenceUtc).getTime()
      );
  });

  readonly activeCount = computed(() =>
    this.schedules().filter(
      item => item.status === RecurringStatus.Active
    ).length
  );

  readonly pausedCount = computed(() =>
    this.schedules().filter(
      item => item.status === RecurringStatus.Paused
    ).length
  );

  readonly upcomingCount = computed(() => {
    const now = Date.now();
    const end = now + 30 * 24 * 60 * 60 * 1000;

    return this.schedules().filter(item => {
      const next = Date.parse(item.nextOccurrenceUtc);
      return item.status === RecurringStatus.Active &&
        next >= now && next <= end &&
        (!item.endDateUtc ||
          next <= Date.parse(item.endDateUtc));
    }).length;
  });

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.loading.set(true);
    this.error.set('');

    forkJoin({
      schedules: this.service.getAll(),
      accounts: this.accountsService.getAccounts(),
      categories: this.transactionsService.getCategories()
    }).pipe(
      finalize(() => this.loading.set(false))
    ).subscribe({
      next: result => {
        this.schedules.set(result.schedules);
        this.accounts.set(result.accounts);
        this.categories.set(result.categories);
      },
      error: err => {
        console.error('Recurring schedule load failed', err);
        this.error.set('Unable to load recurring schedules.');
      }
    });
  }

  accountName(id: string | null): string {
    if (!id) return '—';
    return this.accounts().find(a => a.id === id)?.name
      ?? 'Unknown account';
  }

  currencyFor(item: RecurringTransaction): string {
    return this.accounts().find(
      a => a.id === item.financialAccountId
    )?.currency ?? 'CAD';
  }

  categoryName(id: string | null): string {
    if (!id) return 'Uncategorized';
    return this.categories().find(c => c.id === id)?.name
      ?? 'Uncategorized';
  }

  frequencyLabel(value: RecurrenceFrequency): string {
    switch (value) {
      case RecurrenceFrequency.Daily: return 'Daily';
      case RecurrenceFrequency.Weekly: return 'Weekly';
      case RecurrenceFrequency.Biweekly: return 'Every 2 weeks';
      case RecurrenceFrequency.Monthly: return 'Monthly';
      case RecurrenceFrequency.Yearly: return 'Yearly';
      default: return 'Unknown';
    }
  }

  typeLabel(type: TransactionType): string {
    return type === TransactionType.Income ? 'Income'
      : type === TransactionType.Expense ? 'Expense'
      : 'Transfer';
  }

  statusLabel(status: RecurringStatus): string {
    return status === RecurringStatus.Active ? 'Active'
      : status === RecurringStatus.Paused ? 'Paused'
      : 'Cancelled';
  }

  availableCategories(): TransactionCategory[] {
    const type = Number(this.form.controls.type.value);
    return this.categories().filter(c => c.type === type);
  }

  availableDestinations(): FinancialAccount[] {
    const sourceId = this.form.controls.financialAccountId.value;
    const source = this.accounts().find(a => a.id === sourceId);

    return this.accounts().filter(a =>
      a.id !== sourceId &&
      (!source || a.currency === source.currency)
    );
  }

  onTypeChanged(): void {
    this.form.patchValue({
      categoryId: '',
      destinationAccountId: ''
    });
  }

  onSourceChanged(): void {
    const selected = this.form.controls.destinationAccountId.value;
    if (selected && !this.availableDestinations().some(
      a => a.id === selected
    )) {
      this.form.patchValue({ destinationAccountId: '' });
    }
  }

  openCreate(): void {
    if (this.saving()) return;

    this.editingId.set(null);
    this.formError.set('');

    this.form.reset({
      type: TransactionType.Expense,
      financialAccountId: this.accounts()[0]?.id ?? '',
      destinationAccountId: '',
      categoryId: '',
      amount: 0,
      description: '',
      frequency: RecurrenceFrequency.Monthly,
     startDate: this.toZonedDateTime(
        new Date().toISOString(),
        'America/Toronto'
        ),
      endDate: '',
      timeZoneId: 'America/Toronto'
    });

    this.showForm.set(true);
  }

  startEdit(item: RecurringTransaction): void {
    if (item.status === RecurringStatus.Cancelled) return;

    this.editingId.set(item.id);
    this.formError.set('');

    this.form.reset({
      type: item.type,
      financialAccountId: item.financialAccountId,
      destinationAccountId: item.destinationAccountId ?? '',
      categoryId: item.categoryId ?? '',
      amount: item.amount,
      description: item.description,
      frequency: item.frequency,
      startDate: this.toZonedDateTime(
        item.startDateUtc,
        item.timeZoneId
      ),
      endDate: item.endDateUtc
        ? this.toZonedDateTime(item.endDateUtc, item.timeZoneId)
        : '',
      timeZoneId: item.timeZoneId
    });

    this.showForm.set(true);
  }

  closeForm(): void {
    if (this.saving()) return;
    this.showForm.set(false);
    this.editingId.set(null);
  }

  save(): void {
    if (this.saving()) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const type = Number(value.type) as TransactionType;
    const source = this.accounts().find(
      a => a.id === value.financialAccountId
    );

    if (!source) {
      this.formError.set('Select an account.');
      return;
    }

    if (!Number.isFinite(value.amount) || value.amount <= 0) {
      this.formError.set('Enter an amount greater than zero.');
      return;
    }

    if (!value.description.trim()) {
      this.formError.set('Enter a description.');
      return;
    }

    if (type === TransactionType.Transfer) {
      const destination = this.availableDestinations().find(
        a => a.id === value.destinationAccountId
      );

      if (!destination) {
        this.formError.set(
          'Choose a different account with matching currency.'
        );
        return;
      }
    }

    if (
      type !== TransactionType.Transfer &&
      value.categoryId &&
      !this.categories().some(c =>
        c.id === value.categoryId && c.type === type
      )
    ) {
      this.formError.set(
        'Choose a category matching the transaction type.'
      );
      return;
    }

    if (!this.validTimeZone(value.timeZoneId)) {
      this.formError.set('Choose a valid time zone.');
      return;
    }

    let startDateUtc: string;
    let endDateUtc: string | null;

    try {
      startDateUtc = this.zonedLocalToUtc(
        value.startDate,
        value.timeZoneId
      );
      endDateUtc = value.endDate
        ? this.zonedLocalToUtc(value.endDate, value.timeZoneId)
        : null;
    } catch {
      this.formError.set('Enter valid schedule dates.');
      return;
    }

    if (
      endDateUtc &&
      Date.parse(endDateUtc) < Date.parse(startDateUtc)
    ) {
      this.formError.set(
        'The end date must be after the start date.'
      );
      return;
    }

    const request: SaveRecurringRequest = {
      financialAccountId: source.id,
      destinationAccountId:
        type === TransactionType.Transfer
          ? value.destinationAccountId : null,
      categoryId:
        type === TransactionType.Transfer
          ? null : value.categoryId || null,
      type,
      amount: value.amount,
      description: value.description.trim(),
      frequency: Number(value.frequency) as RecurrenceFrequency,
      startDateUtc,
      endDateUtc,
      timeZoneId: value.timeZoneId
    };

    const editingId = this.editingId();
    const operation = editingId
      ? this.service.update(editingId, request)
      : this.service.create(request);

    this.saving.set(true);
    this.formError.set('');

    operation.pipe(
      finalize(() => this.saving.set(false))
    ).subscribe({
      next: () => {
        this.showForm.set(false);
        this.editingId.set(null);
        this.notice.set(
          editingId ? 'Schedule updated.' : 'Recurring schedule created.'
        );
        this.loadData();
      },
      error: err => {
        console.error('Recurring save failed', err);
        this.formError.set(
          err.error?.message ?? 'Unable to save schedule.'
        );
      }
    });
  }

  requestAction(
    item: RecurringTransaction,
    action: 'pause' | 'resume' | 'cancel'
  ): void {
    this.confirmation.set({
      id: item.id,
      description: item.description,
      action
    });
  }

  closeConfirmation(): void {
    if (!this.workingId()) {
      this.confirmation.set(null);
    }
  }

  confirmAction(): void {
    const operation = this.confirmation();

    if (!operation || this.workingId()) return;

    this.workingId.set(operation.id);
    this.error.set('');

    const request = operation.action === 'pause'
      ? this.service.pause(operation.id)
      : operation.action === 'resume'
        ? this.service.resume(operation.id)
        : this.service.cancel(operation.id);

    request.pipe(
      finalize(() => this.workingId.set(null))
    ).subscribe({
      next: () => {
        this.confirmation.set(null);
    const messages = {
        pause: 'Schedule paused successfully.',
        resume: 'Schedule resumed successfully.',
        cancel: 'Schedule cancelled successfully.'
        };

this.notice.set(messages[operation.action]);
        this.loadData();
      },
      error: err => {
        console.error('Status change failed', err);
        this.confirmation.set(null);
        this.error.set(
          err.error?.message ?? 'Unable to change schedule status.'
        );
      }
    });
  }

  private localDateTime(iso: string): string {
    const date = new Date(iso);
    return new Date(
      date.getTime() - date.getTimezoneOffset() * 60000
    ).toISOString().slice(0, 16);
  }

  private validTimeZone(zone: string): boolean {
    try {
      new Intl.DateTimeFormat('en-US', {
        timeZone: zone
      });
      return true;
    } catch {
      return false;
    }
  }

  private toZonedDateTime(iso: string, zone: string): string {
    const date = new Date(iso);
    const parts = new Intl.DateTimeFormat('en-CA', {
      timeZone: zone,
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      hourCycle: 'h23'
    }).formatToParts(date);

    const get = (type: string) =>
      parts.find(part => part.type === type)?.value ?? '';

    return `${get('year')}-${get('month')}-${get('day')}` +
      `T${get('hour')}:${get('minute')}`;
  }

  private zonedLocalToUtc(local: string, zone: string): string {
    if (!this.validTimeZone(zone)) {
      throw new Error('Invalid time zone');
    }

    const match = local.match(
      /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/
    );

    if (!match) throw new Error('Invalid date');

    const [year, month, day, hour, minute] =
      match.slice(1).map(Number);

    const baseline = Date.UTC(year, month - 1, day, hour, minute);
    if (!Number.isFinite(baseline)) {
      throw new Error('Invalid date');
    }

    const desired = local;
    const candidates: number[] = [];

    // Check UTC offsets around the requested wall-clock time.
    for (let offsetMinutes = -14 * 60;
      offsetMinutes <= 14 * 60;
      offsetMinutes += 15) {
      const candidate = baseline - offsetMinutes * 60000;
      const formatted = this.toZonedDateTime(
        new Date(candidate).toISOString(), zone
      );

      if (formatted === desired) {
        candidates.push(candidate);
      }
    }

    if (candidates.length === 0) {
      throw new Error('Nonexistent or invalid local time');
    }

    // For an ambiguous DST time, choose the later occurrence.
    return new Date(Math.max(...candidates)).toISOString();
  }
}
