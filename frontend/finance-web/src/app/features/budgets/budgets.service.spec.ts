import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { BudgetsService } from './budgets.service';

describe('BudgetsService', () => {
  let service: BudgetsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        BudgetsService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(BudgetsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('loads the current user budgets', () => {
    service.getAll().subscribe();

    const request = http.expectOne('/api/v1/budgets');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('creates a budget with category, currency and monthly limit', () => {
    service.create({
      categoryId: 'category-id',
      currency: 'CAD',
      monthlyLimit: 250
    }).subscribe();

    const request = http.expectOne('/api/v1/budgets');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      categoryId: 'category-id',
      currency: 'CAD',
      monthlyLimit: 250
    });
    request.flush({});
  });
});
