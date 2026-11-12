import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { AuthService, LoginOutcome } from './auth.service';

describe('AuthService two-step sign-in', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('reports that a second factor is needed without signing in', () => {
    let outcome: LoginOutcome | undefined;
    service.login({ email: 'a@example.com', password: 'pw' }).subscribe(x => outcome = x);

    http.expectOne('/api/v1/auth/login').flush({ requiresTwoFactor: true });

    expect(outcome).toEqual({ status: 'twoFactorRequired' });
    expect(service.currentUser()).toBeNull();
  });

  it('signs in after a valid code and refreshes the CSRF token', () => {
    const user = { id: '1', firstName: 'A', lastName: 'B', email: 'a@example.com' };
    service.loginTwoFactor('123456', false).subscribe();

    const request = http.expectOne('/api/v1/auth/login/two-factor');
    expect(request.request.body).toEqual({ code: '123456', isRecoveryCode: false });
    request.flush(user);
    http.expectOne(req => req.url.includes('/security/csrf')).flush({ token: 't' });

    expect(service.currentUser()).toEqual(user);
  });
});
