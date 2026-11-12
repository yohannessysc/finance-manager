
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

  // Set after the password step when the account uses two-factor auth.
  readonly twoFactorStep = signal(false);
  readonly useRecoveryCode = signal(false);

  readonly codeForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.maxLength(20)]]
  });

  readonly form = this.fb.nonNullable.group({
    email: ['', [
      Validators.required,
      Validators.email
    ]],
    password: ['', Validators.required]
  });

  submitCode(): void {
    if (this.submitting()) {
      return;
    }

    if (this.codeForm.invalid) {
      this.codeForm.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set('');

    this.authService.loginTwoFactor(
      this.codeForm.getRawValue().code.trim(),
      this.useRecoveryCode()
    )
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => this.navigateAfterSignIn(),
        error: err => {
          const code = err.error?.code;

          if (code === 'TWO_FACTOR_EXPIRED') {
            this.backToPassword();
            this.error.set('Your sign-in timed out. Please enter your password again.');
            return;
          }

          this.error.set(
            code === 'ACCOUNT_LOCKED'
              ? 'Too many unsuccessful attempts. Please try again later.'
              : this.useRecoveryCode()
                ? 'That recovery code is not valid or was already used.'
                : 'That code is not valid. Check your authenticator app and try again.'
          );
        }
      });
  }

  toggleRecoveryCode(): void {
    this.useRecoveryCode.update(value => !value);
    this.codeForm.reset();
    this.error.set('');
  }

  backToPassword(): void {
    this.twoFactorStep.set(false);
    this.useRecoveryCode.set(false);
    this.codeForm.reset();
    this.form.controls.password.reset();
    this.error.set('');
  }

  private navigateAfterSignIn(): void {
    // Only allow in-app paths to avoid open redirects
    const returnUrl = this.router.parseUrl(this.router.url).queryParams['returnUrl'];
    const target = typeof returnUrl === 'string' && returnUrl.startsWith('/') && !returnUrl.startsWith('//')
      ? returnUrl
      : '/dashboard';
    this.router.navigateByUrl(target);
  }

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
        next: outcome => {
          this.error.set('');
          this.needsVerification.set(false);

          if (outcome.status === 'twoFactorRequired') {
            this.twoFactorStep.set(true);
            return;
          }

          this.navigateAfterSignIn();
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
