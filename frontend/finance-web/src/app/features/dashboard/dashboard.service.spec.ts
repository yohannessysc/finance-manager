import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { DashboardService } from './dashboard.service';

describe('DashboardService', () => {
  let service: DashboardService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        DashboardService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(DashboardService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('loads the dashboard summary', () => {
    service.getSummary().subscribe();

    const request = http.expectOne('/api/v1/dashboard/summary');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });

  it('requests a bounded forecast horizon', () => {
    service.getForecast(60).subscribe();

    const request = http.expectOne('/api/v1/dashboard/forecast?days=60');
    expect(request.request.method).toBe('GET');
    request.flush({
      horizonDays: 60,
      forecastedAtUtc: '2026-11-08T12:00:00.000Z',
      currencies: []
    });
  });
});
