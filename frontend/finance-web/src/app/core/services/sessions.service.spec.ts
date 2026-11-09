import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';

import { SessionsService } from './sessions.service';

describe('SessionsService', () => {
  let service: SessionsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    service = TestBed.inject(SessionsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists sessions', () => {
    service.list().subscribe();

    const req = http.expectOne('/api/v1/sessions');
    expect(req.request.method).toBe('GET');
    req.flush([]);
  });

  it('revokes one session', () => {
    service.revoke('abc').subscribe();

    const req = http.expectOne('/api/v1/sessions/abc');
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });

  it('revokes other sessions', () => {
    service.revokeOthers().subscribe();

    const req = http.expectOne('/api/v1/sessions/revoke-others');
    expect(req.request.method).toBe('POST');
    req.flush({ revoked: 2 });
  });

  it('revokes all sessions', () => {
    service.revokeAll().subscribe();

    const req = http.expectOne('/api/v1/sessions/revoke-all');
    expect(req.request.method).toBe('POST');
    req.flush({ revoked: 3 });
  });
});
