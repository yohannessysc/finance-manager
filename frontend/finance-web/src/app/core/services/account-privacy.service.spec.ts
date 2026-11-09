import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { AccountPrivacyService } from './account-privacy.service';

describe('AccountPrivacyService', () => {
  let service: AccountPrivacyService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AccountPrivacyService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AccountPrivacyService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('downloads the authenticated financial data export as a blob', () => {
    let response: Blob | undefined;

    service.exportFinancialData().subscribe(value => response = value);

    const request = http.expectOne('/api/v1/account-privacy/export');

    expect(request.request.method).toBe('GET');
    expect(request.request.responseType).toBe('blob');

    request.flush(new Blob(['{}'], { type: 'application/json' }));

    expect(response).toBeInstanceOf(Blob);
  });

  it('submits password and explicit confirmation to delete the account', () => {
    service.deleteAccount({
      currentPassword: 'CurrentPassword123!',
      confirmation: 'DELETE'
    }).subscribe();

    const request = http.expectOne(
      '/api/v1/account-privacy/delete-account'
    );

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      currentPassword: 'CurrentPassword123!',
      confirmation: 'DELETE'
    });

    request.flush(null);
  });
});
