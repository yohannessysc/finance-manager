
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  provideHttpClientTesting,
  HttpTestingController
} from '@angular/common/http/testing';

import {
  AccountProfile,
  AccountSettingsService
} from './account-settings.service';

describe('AccountSettingsService', () => {
  let service: AccountSettingsService;
  let http: HttpTestingController;

  const profile: AccountProfile = {
    id: '11111111-1111-4111-8111-111111111111',
    firstName: 'Test',
    lastName: 'User',
    email: 'test@example.com',
    emailConfirmed: true,
    createdAtUtc: '2026-10-23T12:00:00Z'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AccountSettingsService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    service = TestBed.inject(AccountSettingsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    http.verify();
  });

  it('loads the authenticated user profile', () => {
    let result: AccountProfile | undefined;

    service.getProfile().subscribe(value => result = value);

    const request = http.expectOne(
      '/api/v1/account-settings/profile'
    );

    expect(request.request.method).toBe('GET');

    request.flush(profile);

    expect(result).toEqual(profile);
  });

  it('updates first and last name', () => {
    service.updateProfile({
      firstName: 'Updated',
      lastName: 'Name'
    }).subscribe();

    const request = http.expectOne(
      '/api/v1/account-settings/profile'
    );

    expect(request.request.method).toBe('PUT');

    expect(request.request.body).toEqual({
      firstName: 'Updated',
      lastName: 'Name'
    });

    request.flush({
      ...profile,
      firstName: 'Updated',
      lastName: 'Name'
    });
  });

  it('submits current and new password', () => {
    service.changePassword({
      currentPassword: 'PreviousPassword123!',
      newPassword: 'NewPassword123!'
    }).subscribe();

    const request = http.expectOne(
      '/api/v1/account-settings/change-password'
    );

    expect(request.request.method).toBe('POST');

    expect(request.request.body).toEqual({
      currentPassword: 'PreviousPassword123!',
      newPassword: 'NewPassword123!'
    });

    request.flush({
      message: 'Password changed successfully.'
    });
  });

  it('requests an email change with password reauthentication', () => {
    service.requestEmailChange({
      newEmail: 'new@example.com',
      currentPassword: 'CurrentPassword123!'
    }).subscribe();

    const request = http.expectOne(
      '/api/v1/account-settings/change-email'
    );

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      newEmail: 'new@example.com',
      currentPassword: 'CurrentPassword123!'
    });

    request.flush({ message: 'Confirmation sent.' });
  });

  it('confirms a verified email change', () => {
    service.confirmEmailChange(
      'old@example.com',
      'new@example.com',
      'encoded-token'
    ).subscribe();

    const request = http.expectOne(
      '/api/v1/account-settings/confirm-email-change'
    );

    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      email: 'old@example.com',
      newEmail: 'new@example.com',
      token: 'encoded-token'
    });

    request.flush({ message: 'Email updated.' });
  });

  it('propagates invalid-password errors', () => {
    let status: number | undefined;

    service.changePassword({
      currentPassword: 'WrongPassword123!',
      newPassword: 'NewPassword123!'
    }).subscribe({
      error: error => status = error.status
    });

    const request = http.expectOne(
      '/api/v1/account-settings/change-password'
    );

    request.flush(
      { message: 'Password change failed.' },
      {
        status: 400,
        statusText: 'Bad Request'
      }
    );

    expect(status).toBe(400);
  });

  it('propagates unauthorized requests', () => {
    let status: number | undefined;

    service.getProfile().subscribe({
      error: error => status = error.status
    });

    const request = http.expectOne(
      '/api/v1/account-settings/profile'
    );

    request.flush(
      {},
      {
        status: 401,
        statusText: 'Unauthorized'
      }
    );

    expect(status).toBe(401);
  });
});
