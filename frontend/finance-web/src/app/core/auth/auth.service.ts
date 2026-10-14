
import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import {
  Observable,
  tap,
  switchMap,
  map,
  catchError,
  of
} from 'rxjs';

import {
  AuthUser,
  LoginRequest,
  RegisterRequest
} from './auth.models';

import { CsrfService } from './csrf.service';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly csrfService = inject(CsrfService);

  private readonly apiUrl = '/api/v1/auth';

  // Current authenticated user
  readonly currentUser = signal<AuthUser | null>(null);

  readonly loading = signal(false);

  // -------------------------------
  // Register
  // -------------------------------
  register(
    request: RegisterRequest
  ): Observable<AuthUser> {
    return this.http.post<AuthUser>(
      `${this.apiUrl}/register`,
      request
    );
  }

  // -------------------------------
  // Login
  // -------------------------------
  login(
    request: LoginRequest
  ): Observable<AuthUser> {
    return this.http.post<AuthUser>(
      `${this.apiUrl}/login`,
      request
    ).pipe(
      tap(user => {
        this.currentUser.set(user);
        this.csrfService.clearToken();
      }),
      switchMap(user =>
        this.csrfService.refreshToken().pipe(
          map(() => user)
        )
      )
    );
  }

  // -------------------------------
  // Current User
  // -------------------------------
  getCurrentUser(): Observable<AuthUser | null> {
    this.loading.set(true);

    return this.http.get<AuthUser>(
      `${this.apiUrl}/me`
    ).pipe(
      tap(user => {
        this.currentUser.set(user);
        this.loading.set(false);
      }),
      catchError(() => {
        this.currentUser.set(null);
        this.loading.set(false);
        return of(null);
      })
    );
  }

  // -------------------------------
  // Logout
  // -------------------------------
  logout(): Observable<void> {
    return this.http.post<void>(
      `${this.apiUrl}/logout`,
      {}
    ).pipe(
      tap(() => {
        this.currentUser.set(null);
        this.csrfService.clearToken();
      })
    );
  }

  // -------------------------------
  // Authentication State
  // -------------------------------
  isAuthenticated(): boolean {
    return this.currentUser() !== null;
  }
}
