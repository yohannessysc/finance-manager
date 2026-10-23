
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  ValidatorFn,
  Validators
} from '@angular/forms';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';
import { ThemeService } from '../../../core/services/theme.service';

const passwordsMatch: ValidatorFn = (
  control: AbstractControl
): ValidationErrors | null => {
  const password = control.get('newPassword')?.value;
  const confirmation = control.get('confirmPassword')?.value;

  return password === confirmation
    ? null
    : { passwordsMismatch: true };
};

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './reset-password.html',
  styleUrl: './reset-password.scss'
})
export class ResetPassword {
  private readonly fb = inject(FormBuilder);
  private readonly route = inject(ActivatedRoute);
  private readonly auth = inject(AuthService);

  readonly theme = inject(ThemeService);
  readonly submitting = signal(false);
  readonly completed = signal(false);
  readonly showPassword = signal(false);
  readonly error = signal('');

  readonly email =
    this.route.snapshot.queryParamMap.get('email') ?? '';

  private readonly token =
    this.route.snapshot.queryParamMap.get('token') ?? '';

  readonly validLink = !!this.email && !!this.token;

  readonly form = this.fb.nonNullable.group(
    {
      newPassword: [
        '',
        [
          Validators.required,
          Validators.minLength(12),
          Validators.pattern(
            /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$/
          )
        ]
      ],
      confirmPassword: ['', Validators.required]
    },
    { validators: passwordsMatch }
  );

  togglePassword(): void {
    this.showPassword.update(value => !value);
  }

  submit(): void {
    if (!this.validLink || this.submitting() || this.completed()) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set('');

    this.auth.resetPassword(
      this.email,
      this.token,
      this.form.controls.newPassword.value
    )
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({
        next: () => {
          this.completed.set(true);
          this.form.reset();
        },
        error: err => {
          if (err.status === 400) {
            this.error.set(
              'The link may have expired or the password does not meet the requirements. Request a new reset link if needed.'
            );
          } else {
            this.error.set(
              'We could not reset your password right now. Please try again.'
            );
          }
        }
      });
  }
}
