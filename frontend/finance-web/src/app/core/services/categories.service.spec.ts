
import { TestBed } from '@angular/core/testing';
import {
  provideHttpClient
} from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import {
  CategoriesService,
  Category,
  CategoryTypeOption,
  CategoryUsage
} from './categories.service';

describe('CategoriesService', () => {
  let service: CategoriesService;
  let httpMock: HttpTestingController;

  const categoryId =
    '85aa25f6-907f-4b36-80c1-c40aabc91551';

  const category: Category = {
    id: categoryId,
    name: 'Travel',
    type: 20,
    createdAtUtc: '2026-10-23T12:00:00Z'
  };

  const types: CategoryTypeOption[] = [
    { name: 'Expense', value: 20 },
    { name: 'Income', value: 10 }
  ];

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        CategoriesService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(CategoriesService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loads categories', () => {
    let result: Category[] | undefined;

    service.getAll().subscribe(value => result = value);

    const req = httpMock.expectOne('/api/v1/categories');

    expect(req.request.method).toBe('GET');

    req.flush([category]);

    expect(result).toEqual([category]);
  });

  it('loads category types from the backend', () => {
    let result: CategoryTypeOption[] | undefined;

    service.getTypes().subscribe(value => result = value);

    const req = httpMock.expectOne('/api/v1/categories/types');

    expect(req.request.method).toBe('GET');

    req.flush(types);

    expect(result).toEqual(types);
  });

  it('loads category usage counts', () => {
    const usage: CategoryUsage[] = [{
      categoryId,
      transactionCount: 3,
      recurringCount: 1,
      inUse: true
    }];

    let result: CategoryUsage[] | undefined;

    service.getUsage().subscribe(value => result = value);

    const req = httpMock.expectOne('/api/v1/categories/usage');

    expect(req.request.method).toBe('GET');

    req.flush(usage);

    expect(result).toEqual(usage);
  });

  it('creates a category with its selected type', () => {
    let result: Category | undefined;

    service.create({
      name: 'Travel',
      type: 20
    }).subscribe(value => result = value);

    const req = httpMock.expectOne('/api/v1/categories');

    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({
      name: 'Travel',
      type: 20
    });

    req.flush(category);

    expect(result).toEqual(category);
  });

  it('updates the category name without changing type', () => {
    service.update(categoryId, {
      name: 'Vacation'
    }).subscribe();

    const req = httpMock.expectOne(
      `/api/v1/categories/${categoryId}`
    );

    expect(req.request.method).toBe('PUT');
    expect(req.request.body).toEqual({
      name: 'Vacation'
    });

    req.flush({
      ...category,
      name: 'Vacation'
    });
  });

  it('deletes a category using its ID', () => {
    let completed = false;

    service.delete(categoryId).subscribe({
      complete: () => completed = true
    });

    const req = httpMock.expectOne(
      `/api/v1/categories/${categoryId}`
    );

    expect(req.request.method).toBe('DELETE');

    req.flush(null, {
      status: 204,
      statusText: 'No Content'
    });

    expect(completed).toBe(true);
  });

  it('propagates duplicate-category validation errors', () => {
    let errorStatus: number | undefined;

    service.create({
      name: 'Travel',
      type: 20
    }).subscribe({
      error: error => errorStatus = error.status
    });

    const req = httpMock.expectOne('/api/v1/categories');

    req.flush(
      { message: 'A category with this name already exists.' },
      {
        status: 400,
        statusText: 'Bad Request'
      }
    );

    expect(errorStatus).toBe(400);
  });

  it('propagates 409 when deleting an in-use category', () => {
    let errorStatus: number | undefined;

    service.delete(categoryId).subscribe({
      error: error => errorStatus = error.status
    });

    const req = httpMock.expectOne(
      `/api/v1/categories/${categoryId}`
    );

    req.flush(
      { message: 'Category is in use.' },
      {
        status: 409,
        statusText: 'Conflict'
      }
    );

    expect(errorStatus).toBe(409);
  });
});
