import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from './auth.service';

/**
 * When the server says the session is gone (401), clear local auth state and
 * send the user to login, returning them to where they were afterwards.
 * Auth endpoints are excluded: a 401 from /login means bad credentials, and
 * /me is used by the guard to probe the session.
 */
export const sessionExpiredInterceptor: HttpInterceptorFn = (request, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);

  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        request.url.startsWith('/api/') &&
        !request.url.startsWith('/api/v1/auth/')
      ) {
        authService.currentUser.set(null);
        const returnUrl = router.url.startsWith('/login') ? undefined : router.url;
        router.navigate(['/login'], {
          queryParams: { returnUrl, reason: 'expired' }
        });
      }
      return throwError(() => error);
    })
  );
};
