
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { AuthService } from '../../../core/auth/auth.service';

type VerificationState = 'ready' | 'loading' | 'success' | 'error';

@Component({
  selector: 'app-confirm-email',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './confirm-email.html',
  styleUrl: './confirm-email.scss'
})
export class ConfirmEmail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);

  readonly status = signal<VerificationState>('ready');
  readonly error = signal('');

  readonly email =
    this.route.snapshot.queryParamMap.get('email') ?? '';

  private readonly token =
    this.route.snapshot.queryParamMap.get('token') ?? '';

  ngOnInit(): void {
    if (!this.email || !this.token) {
      this.status.set('error');
      this.error.set(
        'This verification link is incomplete. Please open the original link.'
      );
    }
  }

  confirm(): void {
    if (this.status() !== 'ready') return;

    this.status.set('loading');
    this.error.set('');

    this.authService.confirmEmail(this.email, this.token)
      .subscribe({
        next: () => {
          this.status.set('success');
        },
        error: err => {
          this.status.set('error');

          if (err.status === 400) {
            this.error.set(
              'This verification link is invalid or has expired.'
            );
          } else {
            this.error.set(
              'We could not verify your email. Please try again.'
            );
          }
        }
      });
  }

  retry(): void {
    if (this.email && this.token) {
      this.status.set('ready');
      this.error.set('');
    }
  }
}
