
import { inject } from '@angular/core';

import {
  HttpContextToken,
  HttpInterceptorFn
} from '@angular/common/http';

import { switchMap } from 'rxjs';

import { CsrfService } from './csrf.service';

export const SKIP_CSRF = new HttpContextToken<boolean>(
  () => false
);

export const csrfInterceptor: HttpInterceptorFn = (
  request,
  next
) => {
  const method = request.method.toUpperCase();

  const isSafeMethod = [
    'GET',
    'HEAD',
    'OPTIONS'
  ].includes(method);

  const isApiRequest =
    request.url.startsWith('/api/');

  if (
    isSafeMethod ||
    !isApiRequest ||
    request.context.get(SKIP_CSRF)
  ) {
    return next(request);
  }

  const csrfService = inject(CsrfService);

  const token = csrfService.getToken();

  if (token) {
    return next(
      request.clone({
        setHeaders: {
          'X-XSRF-TOKEN': token
        }
      })
    );
  }

  return csrfService.refreshToken().pipe(
    switchMap(newToken =>
      next(
        request.clone({
          setHeaders: {
            'X-XSRF-TOKEN': newToken
          }
        })
      )
    )
  );
};
