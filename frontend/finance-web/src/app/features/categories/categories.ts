
import {
  Component,
  computed,
  inject,
  signal
} from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { forkJoin, finalize } from 'rxjs';

import {
  Category,
  CategoryTypeOption,
  CategoryUsage,
  CategoriesService
} from '../../core/services/categories.service';

type CategoryTab = 'Expense' | 'Income';

@Component({
  selector: 'app-categories',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './categories.html',
  styleUrl: './categories.scss'
})
export class Categories {
  private readonly service = inject(CategoriesService);
  private readonly fb = inject(FormBuilder);

  readonly categories = signal<Category[]>([]);
  readonly types = signal<CategoryTypeOption[]>([]);
  readonly usages = signal<CategoryUsage[]>([]);
  readonly usageLoaded = signal(false);

  readonly activeTab = signal<CategoryTab>('Expense');
  readonly search = signal('');

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly deleting = signal(false);

  readonly error = signal('');
  readonly formError = signal('');
  readonly notice = signal('');

  readonly editorOpen = signal(false);
  readonly editingId = signal<string | null>(null);
  readonly pendingDelete = signal<Category | null>(null);

  readonly form = this.fb.nonNullable.group({
    name: ['', [
      Validators.required,
      Validators.maxLength(150)
    ]],
    type: ['', Validators.required]
  });

  readonly usageByCategory = computed(() => {
    return new Map(
      this.usages().map(usage => [
        usage.categoryId,
        usage
      ])
    );
  });

  readonly filteredCategories = computed(() => {
    const tab = this.activeTab();
    const query = this.search().trim().toLowerCase();
    const type = this.types().find(t => t.name === tab);

    if (!type) return [];

    return this.categories()
      .filter(category =>
        category.type === type.value &&
        category.name.toLowerCase().includes(query)
      )
      .sort((a, b) => a.name.localeCompare(b.name));
  });

  readonly expenseCount = computed(() => this.countFor('Expense'));
  readonly incomeCount = computed(() => this.countFor('Income'));

  constructor() {
    this.load();
  }

  private countFor(tab: CategoryTab): number {
    const option = this.types().find(item => item.name === tab);

    return option
      ? this.categories().filter(
          category => category.type === option.value
        ).length
      : 0;
  }

  usageFor(categoryId: string): CategoryUsage | undefined {
    return this.usageByCategory().get(categoryId);
  }

  isDeletable(categoryId: string): boolean {
    const usage = this.usageFor(categoryId);

    return this.usageLoaded() &&
      usage !== undefined &&
      !usage.inUse &&
      !this.deleting();
  }

  load(): void {
    this.loading.set(true);
    this.usageLoaded.set(false);
    this.error.set('');

    forkJoin({
      categories: this.service.getAll(),
      types: this.service.getTypes(),
      usages: this.service.getUsage()
    })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: result => {
          this.categories.set(result.categories);
          this.types.set(result.types);
          this.usages.set(result.usages);
          this.usageLoaded.set(true);
        },
        error: () => {
          this.error.set(
            'Unable to load categories and usage information. Please retry.'
          );
        }
      });
  }

  selectTab(tab: CategoryTab): void {
    this.activeTab.set(tab);
    this.search.set('');
    this.notice.set('');
    this.error.set('');
  }

  updateSearch(event: Event): void {
    const target = event.target as HTMLInputElement;
    this.search.set(target.value);
  }

  openCreate(): void {
    const type = this.types().find(
      item => item.name === this.activeTab()
    );

    if (!type) return;

    this.editingId.set(null);
    this.form.reset({
      name: '',
      type: String(type.value)
    });

    this.form.controls.type.enable();
    this.formError.set('');
    this.editorOpen.set(true);
  }

  openEdit(category: Category): void {
    this.editingId.set(category.id);

    this.form.reset({
      name: category.name,
      type: String(category.type)
    });

    this.form.controls.type.disable();
    this.formError.set('');
    this.editorOpen.set(true);
  }

  closeEditor(): void {
    if (this.saving()) return;

    this.editorOpen.set(false);
    this.editingId.set(null);
    this.form.controls.type.enable();
    this.formError.set('');
  }

  save(): void {
    if (this.saving()) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const name = this.form.controls.name.value.trim();

    if (!name) {
      this.form.controls.name.setErrors({ required: true });
      this.form.controls.name.markAsTouched();
      return;
    }

    const editingId = this.editingId();

    this.saving.set(true);
    this.formError.set('');

    const request$ = editingId
      ? this.service.update(editingId, { name })
      : this.service.create({
          name,
          type: Number(this.form.getRawValue().type)
        });

    request$
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: category => {
          if (editingId) {
            this.categories.update(items =>
              items.map(item =>
                item.id === category.id ? category : item
              )
            );
          } else {
            this.categories.update(items => [
              ...items,
              category
            ]);

            this.usages.update(items => [
              ...items,
              {
                categoryId: category.id,
                transactionCount: 0,
                recurringCount: 0,
                inUse: false
              }
            ]);
          }

          this.editorOpen.set(false);
          this.editingId.set(null);
          this.form.controls.type.enable();

          this.notice.set(
            editingId
              ? 'Category updated successfully.'
              : 'Category created successfully.'
          );
        },
        error: (err: HttpErrorResponse) => {
          this.formError.set(
            this.errorMessage(err, 'Unable to save category.')
          );
        }
      });
  }

  requestDelete(category: Category): void {
    if (!this.isDeletable(category.id)) return;

    this.pendingDelete.set(category);
    this.error.set('');
  }

  cancelDelete(): void {
    if (this.deleting()) return;

    this.pendingDelete.set(null);
  }

  confirmDelete(): void {
    const category = this.pendingDelete();

    if (
      !category ||
      this.deleting() ||
      !this.isDeletable(category.id)
    ) {
      return;
    }

    this.deleting.set(true);
    this.error.set('');

    this.service.delete(category.id)
      .pipe(finalize(() => this.deleting.set(false)))
      .subscribe({
        next: () => {
          this.categories.update(items =>
            items.filter(item => item.id !== category.id)
          );

          this.usages.update(items =>
            items.filter(item => item.categoryId !== category.id)
          );

          this.pendingDelete.set(null);
          this.notice.set('Category deleted successfully.');
        },
        error: (err: HttpErrorResponse) => {
          this.pendingDelete.set(null);

          if (err.status === 409) {
            this.error.set(
              'This category is now used by transactions or recurring schedules and cannot be deleted.'
            );

            // Refresh usage because another transaction
            // may have been added since the page loaded.
            this.load();
            return;
          }

          this.error.set(
            this.errorMessage(err, 'Unable to delete category.')
          );
        }
      });
  }

  private errorMessage(
    err: HttpErrorResponse,
    fallback: string
  ): string {
    if (
      typeof err.error?.message === 'string' &&
      err.error.message.trim()
    ) {
      return err.error.message;
    }

    if (err.status === 401) {
      return 'Your session has expired. Please sign in again.';
    }

    if (err.status === 429) {
      return 'Too many requests. Please try again shortly.';
    }

    return fallback;
  }

  iconFor(category: Category): string {
    const name = category.name.toLowerCase();

    if (/rent|housing|mortgage/.test(name)) return '⌂';
    if (/grocer|food/.test(name)) return '◈';
    if (/transport|fuel|gas|car/.test(name)) return '↗';
    if (/salary|payroll/.test(name)) return '$';
    if (/business|freelance|rideshare/.test(name)) return '✦';
    if (/health|medical/.test(name)) return '✚';
    if (/shopping/.test(name)) return '▣';
    if (/entertain|restaurant/.test(name)) return '♫';
    if (/investment/.test(name)) return '↗';
    if (/education/.test(name)) return '▤';

    return '◉';
  }
}
