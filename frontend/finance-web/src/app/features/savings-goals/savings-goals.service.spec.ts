import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { SavingsGoalsService } from './savings-goals.service';

describe('SavingsGoalsService', () => {
  let service: SavingsGoalsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        SavingsGoalsService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(SavingsGoalsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('loads the current user goals', () => {
    service.getAll().subscribe();

    const request = http.expectOne('/api/v1/savings-goals');
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('records a contribution against a goal', () => {
    service.addContribution('goal-id', {
      amount: 25,
      contributionDateUtc: '2026-11-08T12:00:00.000Z',
      note: 'Payday'
    }).subscribe();

    const request = http.expectOne(
      '/api/v1/savings-goals/goal-id/contributions'
    );
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      amount: 25,
      contributionDateUtc: '2026-11-08T12:00:00.000Z',
      note: 'Payday'
    });
    request.flush({});
  });

  it('loads contribution history for one goal', () => {
    service.getContributions('goal-id').subscribe();

    const request = http.expectOne(
      '/api/v1/savings-goals/goal-id/contributions'
    );
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });
});
