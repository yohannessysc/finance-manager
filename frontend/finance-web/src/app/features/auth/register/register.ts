
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
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: './register.scss'
})
export class Register {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly theme = inject(ThemeService);

  readonly submitting = signal(false);
  readonly showPassword = signal(false);
  readonly error = signal('');

  readonly form = this.fb.nonNullable.group({
    firstName: [
      '',
      [Validators.required, Validators.maxLength(100)]
    ],
    lastName: [
      '',
      [Validators.required, Validators.maxLength(100)]
    ],
    email: [
      '',
      [Validators.required, Validators.email]
    ],
    password: [
      '',
      [
        Validators.required,
        Validators.minLength(12),
        Validators.pattern(
          /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$/
        )
      ]
    ]
  });

  togglePassword(): void {
    this.showPassword.update(value => !value);
  }

  submit(): void {
    if (this.submitting()) return;

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.error.set('');

    this.authService.register(this.form.getRawValue())
      .pipe(finalize(() => this.submitting.set(false)))
      .subscribe({

        next: () => {
        this.router.navigate(['/verify-email'], {
            queryParams: {
            email: this.form.controls.email.value.trim()
            }
        });
        },

        error: err => {
          const errors = err.error?.errors;
          const message = err.error?.message;

          this.error.set(
            Array.isArray(errors)
              ? errors.join(' ')
              : typeof message === 'string'
                ? message
                : 'Unable to create your account. Please try again.'
          );
        }
      });
  }
}
