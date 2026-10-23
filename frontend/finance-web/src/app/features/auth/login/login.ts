
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { ThemeService } from '../../../core/services/theme.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class Login {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  // Apply the saved theme on authentication pages.
  readonly theme = inject(ThemeService);

  readonly submitting = signal(false);
  readonly showPassword = signal(false);
  readonly error = signal('');
  readonly needsVerification = signal(false);

  readonly form = this.fb.nonNullable.group({
    email: ['', [
      Validators.required,
      Validators.email
    ]],
    password: ['', Validators.required]
  });

  togglePassword(): void {
    this.showPassword.update(value => !value);
  }

  submit(): void {
    if (this.submitting()) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    // Reset previous login messages.
    this.submitting.set(true);
    this.error.set('');
    this.needsVerification.set(false);

    const credentials = this.form.getRawValue();

    this.authService.login(credentials)
      .pipe(
        finalize(() => this.submitting.set(false))
      )
      .subscribe({
        next: () => {
          this.error.set('');
          this.needsVerification.set(false);

          this.router.navigateByUrl('/dashboard');
        },

        error: err => {
          const code = err.error?.code;

          // Correct password, but email not verified.
          if (code === 'EMAIL_NOT_VERIFIED') {
            this.needsVerification.set(true);
            this.error.set('');
            return;
          }

          this.needsVerification.set(false);

          // Account temporarily locked after failed attempts.
          if (code === 'ACCOUNT_LOCKED') {
            this.error.set(
              'Too many unsuccessful login attempts. ' +
              'Please try again later.'
            );
            return;
          }

          // Wrong email or password.
          if (
            code === 'INVALID_CREDENTIALS' ||
            err.status === 401
          ) {
            this.error.set(
              'Invalid email or password. ' +
              'Please check your credentials and try again.'
            );
            return;
          }

          // Other server or network failures.
          this.error.set(
            'We could not sign you in right now. ' +
            'Please try again later.'
          );
        }
      });
  }
}
