
import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';

import { AuthService } from '../../../core/auth/auth.service';

@Component({
  selector: 'app-verify-email',
  standalone: true,
  imports: [RouterLink, FormsModule],
  templateUrl: './verify-email.html',
  styleUrl: './verify-email.scss'
})
export class VerifyEmail {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);

  readonly email = signal(
    this.route.snapshot.queryParamMap.get('email') ?? ''
  );

  readonly sending = signal(false);
  readonly success = signal(false);
  readonly error = signal('');

  resend(): void {
    const email = this.email().trim();

    if (this.sending()) return;

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      this.error.set('Please enter a valid email address.');
      return;
    }

    this.sending.set(true);
    this.success.set(false);
    this.error.set('');

    this.authService.resendConfirmation(email)
      .pipe(finalize(() => this.sending.set(false)))
      .subscribe({
        next: () => {
          this.success.set(true);
        },
        error: () => {
          this.error.set(
            'Unable to process your request. Please try again.'
          );
        }
      });
  }
}
