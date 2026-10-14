
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map, of } from 'rxjs';

import { AuthService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isAuthenticated()) {
    return true;
  }

  return authService.getCurrentUser().pipe(
    map(user =>
      user !== null
        ? true
        : router.createUrlTree(['/login'])
    )
  );
};
