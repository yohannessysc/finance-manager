
import { Component, inject, signal } from '@angular/core';
import {
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet
} from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import {
  ThemePreference,
  ThemeService
} from '../../core/services/theme.service';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive
  ],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.scss'
})
export class AppShell {
  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);

  private readonly router = inject(Router);

  readonly mobileMenuOpen = signal(false);
  readonly loggingOut = signal(false);
  readonly logoutError = signal('');

  closeMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  toggleMenu(): void {
    this.mobileMenuOpen.update(open => !open);
  }

  changeTheme(value: string): void {
    if (
      value === 'light' ||
      value === 'dark' ||
      value === 'system'
    ) {
      this.theme.setTheme(value as ThemePreference);
    }
  }

  logout(): void {
    if (this.loggingOut()) return;

    this.loggingOut.set(true);
    this.logoutError.set('');

    this.auth.logout().subscribe({
      next: () => {
        this.loggingOut.set(false);
        this.router.navigateByUrl('/login');
      },
      error: () => {
        this.loggingOut.set(false);
        this.logoutError.set(
          'Unable to sign out. Please try again.'
        );
      }
    });
  }
}
