import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';

import { ReportsService } from './reports.service';

describe('ReportsService', () => {
  let service: ReportsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ReportsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests the category report for a month range and currency', () => {
    service.getCategoryReport('2026-01', '2026-06', 'CAD').subscribe();

    const request = http.expectOne(r => r.url === '/api/v1/reports/categories');
    expect(request.request.params.get('from')).toBe('2026-01');
    expect(request.request.params.get('to')).toBe('2026-06');
    expect(request.request.params.get('currency')).toBe('CAD');
    request.flush({});
  });

  it('lets the server pick the currency when none is chosen', () => {
    service.getCategoryReport('2026-01', '2026-06', null).subscribe();

    const request = http.expectOne(r => r.url === '/api/v1/reports/categories');
    expect(request.request.params.has('currency')).toBe(false);
    request.flush({});
  });
});
