import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, of } from 'rxjs';

import { AuthService } from './auth.service';

// Public marketing pages: signed-in users go straight to their dashboard.
export const guestGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  return authService.getCurrentUser().pipe(
    map(user => user === null ? true : router.createUrlTree(['/dashboard'])),
    catchError(() => of(true))
  );
};
