import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import {
  BillingInterval,
  BillingService,
  SubscriptionPlan
} from './billing.service';

describe('BillingService', () => {
  let service: BillingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(BillingService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts checkout for the chosen plan and interval', () => {
    service.checkout(SubscriptionPlan.Bank).subscribe(result =>
      expect(result.url).toBe('https://checkout.stripe.test'));

    const request = http.expectOne('/api/v1/billing/checkout');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      plan: SubscriptionPlan.Bank,
      interval: BillingInterval.Monthly
    });
    request.flush({ url: 'https://checkout.stripe.test' });
  });

  it('opens the billing portal', () => {
    service.portal().subscribe();

    const request = http.expectOne('/api/v1/billing/portal');
    expect(request.request.method).toBe('POST');
    request.flush({ url: 'https://billing.stripe.test' });
  });
});
