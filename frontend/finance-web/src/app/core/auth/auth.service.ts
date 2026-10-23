
import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';

import {
  Observable,
  tap,
  switchMap,
  map,
  catchError,
  finalize,
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

  readonly currentUser = signal<AuthUser | null>(null);
  readonly loading = signal(false);

  // ---------------------------------------
  // Registration
  // ---------------------------------------
  register(
    request: RegisterRequest
  ): Observable<AuthUser> {
    return this.http.post<AuthUser>(
      `${this.apiUrl}/register`,
      request
    );
  }

  // ---------------------------------------
  // Login
  // ---------------------------------------
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

  // ---------------------------------------
  // Verify current session with the server
  // ---------------------------------------
  getCurrentUser(): Observable<AuthUser | null> {
    this.loading.set(true);

    return this.http.get<AuthUser>(
      `${this.apiUrl}/me`
    ).pipe(
      tap(user => {
        this.currentUser.set(user);
      }),
      map(user => user as AuthUser | null),
      catchError(() => {
        // Never continue treating a failed session
        // check as proof that the user is logged in.
        this.currentUser.set(null);
        return of(null);
      }),
      finalize(() => {
        this.loading.set(false);
      })
    );
  }

  // ---------------------------------------
  // Logout
  // ---------------------------------------
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

  // ---------------------------------------
  // Email verification
  // ---------------------------------------
  confirmEmail(
    email: string,
    token: string
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.apiUrl}/confirm-email`,
      { email, token }
    );
  }

  resendConfirmation(
    email: string
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.apiUrl}/resend-confirmation`,
      { email }
    );
  }

  // ---------------------------------------
  // Password recovery
  // ---------------------------------------
  forgotPassword(
    email: string
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.apiUrl}/forgot-password`,
      { email }
    );
  }

  resetPassword(
    email: string,
    token: string,
    newPassword: string
  ): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(
      `${this.apiUrl}/reset-password`,
      { email, token, newPassword }
    );
  }

  // ---------------------------------------
  // Cached UI authentication state
  // ---------------------------------------
  isAuthenticated(): boolean {
    return this.currentUser() !== null;
  }
}
