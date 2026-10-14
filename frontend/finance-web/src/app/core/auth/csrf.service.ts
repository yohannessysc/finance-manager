
import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Observable, map, tap } from 'rxjs';

import { SKIP_CSRF } from './csrf.interceptor';

interface CsrfResponse {
  token: string;
}

@Injectable({
  providedIn: 'root'
})
export class CsrfService {
  private readonly http = inject(HttpClient);

  private token: string | null = null;

  getToken(): string | null {
    return this.token;
  }

  clearToken(): void {
    this.token = null;
  }

  refreshToken(): Observable<string> {
    this.clearToken();

    return this.http.get<CsrfResponse>(
      '/api/v1/security/csrf',
      {
        context: new HttpContext().set(SKIP_CSRF, true)
      }
    ).pipe(
      map(response => response.token),
      tap(token => {
        this.token = token;
      })
    );
  }
}
