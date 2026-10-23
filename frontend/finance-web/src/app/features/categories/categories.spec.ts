
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { Categories } from './categories';
import {
  CategoriesService,
  Category,
  CategoryUsage,
  CategoryTypeOption
} from '../../core/services/categories.service';

describe('Categories', () => {
  const groceries: Category = {
    id: '11111111-1111-4111-8111-111111111111',
    name: 'Groceries',
    type: 20,
    createdAtUtc: '2026-10-23T12:00:00Z'
  };

  const travel: Category = {
    id: '22222222-2222-4222-8222-222222222222',
    name: 'Travel',
    type: 20,
    createdAtUtc: '2026-10-23T12:00:00Z'
  };

  const salary: Category = {
    id: '33333333-3333-4333-8333-333333333333',
    name: 'Salary',
    type: 10,
    createdAtUtc: '2026-10-23T12:00:00Z'
  };

  const types: CategoryTypeOption[] = [
    { name: 'Expense', value: 20 },
    { name: 'Income', value: 10 }
  ];

  const usages: CategoryUsage[] = [
    {
      categoryId: groceries.id,
      transactionCount: 3,
      recurringCount: 1,
      inUse: true
    },
    {
      categoryId: travel.id,
      transactionCount: 0,
      recurringCount: 0,
      inUse: false
    },
    {
      categoryId: salary.id,
      transactionCount: 1,
      recurringCount: 0,
      inUse: true
    }
  ];

  let mockService: {
    getAll: ReturnType<typeof vi.fn>;
    getTypes: ReturnType<typeof vi.fn>;
    getUsage: ReturnType<typeof vi.fn>;
    create: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
    delete: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    mockService = {
      getAll: vi.fn().mockReturnValue(
        of([groceries, travel, salary])
      ),
      getTypes: vi.fn().mockReturnValue(of(types)),
      getUsage: vi.fn().mockReturnValue(of(usages)),
      create: vi.fn().mockReturnValue(of(travel)),
      update: vi.fn().mockReturnValue(of(travel)),
      delete: vi.fn().mockReturnValue(of(void 0))
    };

    await TestBed.configureTestingModule({
      imports: [Categories],
      providers: [
        {
          provide: CategoriesService,
          useValue: mockService
        }
      ]
    }).compileComponents();
  });

  function setup() {
    const fixture = TestBed.createComponent(Categories);

    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      element: fixture.nativeElement as HTMLElement
    };
  }

  it('loads categories, types, and usage information', () => {
    const { component } = setup();

    expect(mockService.getAll).toHaveBeenCalledOnce();
    expect(mockService.getTypes).toHaveBeenCalledOnce();
    expect(mockService.getUsage).toHaveBeenCalledOnce();

    expect(component.categories()).toHaveLength(3);
    expect(component.usageLoaded()).toBe(true);
  });

  it('shows expense categories by default', () => {
    const { component } = setup();

    const names = component.filteredCategories()
      .map(category => category.name);

    expect(names).toEqual(['Groceries', 'Travel']);
    expect(names).not.toContain('Salary');
  });

  it('switches between expense and income categories', () => {
    const { component } = setup();

    component.selectTab('Income');

    expect(
      component.filteredCategories().map(x => x.name)
    ).toEqual(['Salary']);
  });

  it('filters categories using a case-insensitive search', () => {
    const { component } = setup();

    component.search.set('GROC');

    expect(
      component.filteredCategories().map(x => x.name)
    ).toEqual(['Groceries']);
  });

  it('shows actual transaction and recurring usage counts', () => {
    const { component, element } = setup();

    const usage = component.usageFor(groceries.id);

    expect(usage?.transactionCount).toBe(3);
    expect(usage?.recurringCount).toBe(1);
    expect(usage?.inUse).toBe(true);

    expect(element.textContent).toContain('3');
    expect(element.textContent).toContain('In use');
  });

  it('prevents deletion of a category that is in use', () => {
    const { component, element } = setup();

    expect(component.isDeletable(groceries.id)).toBe(false);

    const deleteButton = element.querySelector(
      `[aria-label="Delete Groceries"]`
    ) as HTMLButtonElement | null;

    expect(deleteButton).not.toBeNull();
    expect(deleteButton?.disabled).toBe(true);

    component.requestDelete(groceries);

    expect(component.pendingDelete()).toBeNull();
    expect(mockService.delete).not.toHaveBeenCalled();
  });

  it('allows an unused category to be selected for deletion', () => {
    const { component } = setup();

    expect(component.isDeletable(travel.id)).toBe(true);

    component.requestDelete(travel);

    expect(component.pendingDelete()?.id).toBe(travel.id);
  });

  it('deletes an unused category after confirmation', () => {
    const { component } = setup();

    component.requestDelete(travel);
    component.confirmDelete();

    expect(mockService.delete).toHaveBeenCalledWith(travel.id);

    expect(
      component.categories().some(x => x.id === travel.id)
    ).toBe(false);

    expect(component.pendingDelete()).toBeNull();
  });

  it('requires a category name before saving', () => {
    const { component } = setup();

    component.openCreate();

    component.form.controls.name.setValue('');

    component.save();

    expect(component.form.controls.name.invalid).toBe(true);
    expect(mockService.create).not.toHaveBeenCalled();
  });

  it('rejects category names longer than 150 characters', () => {
    const { component } = setup();

    component.openCreate();

    component.form.controls.name.setValue('X'.repeat(151));

    component.save();

    expect(component.form.invalid).toBe(true);
    expect(mockService.create).not.toHaveBeenCalled();
  });

  it('creates a category with the backend-defined type', () => {
    const { component } = setup();

    component.openCreate();

    component.form.controls.name.setValue('Travel');

    component.save();

    expect(mockService.create).toHaveBeenCalledWith({
      name: 'Travel',
      type: 20
    });

    expect(component.editorOpen()).toBe(false);
  });

  it('disables changing category type during editing', () => {
    const { component } = setup();

    component.openEdit(groceries);

    expect(component.editingId()).toBe(groceries.id);
    expect(component.form.controls.type.disabled).toBe(true);
  });
});
