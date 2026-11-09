import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { AccountSettingsService } from '../../../core/services/account-settings.service';

type ConfirmationState = 'ready' | 'loading' | 'success' | 'error';

@Component({
  selector: 'app-confirm-email-change',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="confirmation-page">
      <section class="confirmation-card">
        <span class="eyebrow">ACCOUNT SECURITY</span>
        <h1>Confirm email change</h1>

        @if (status() === 'ready') {
          <p>Confirm the new address {{ newEmail }} for your account.</p>
          <button type="button" (click)="confirm()">Confirm email</button>
        } @else if (status() === 'loading') {
          <p role="status">Confirming your new email address...</p>
        } @else if (status() === 'success') {
          <p role="status">{{ message() }}</p>
          <a routerLink="/login">Sign in with your new email</a>
        } @else {
          <p role="alert">{{ message() }}</p>
          <a routerLink="/settings">Return to settings</a>
        }
      </section>
    </main>
  `,
  styles: [`
    :host { display: block; min-height: 100vh; padding: 48px 20px; }
    .confirmation-page { max-width: 520px; margin: 0 auto; }
    .confirmation-card { padding: 28px; border: 1px solid var(--fm-border); border-radius: 16px; background: var(--fm-surface); }
    .eyebrow { color: var(--fm-primary); font-size: 10px; font-weight: 800; letter-spacing: 1.5px; }
    h1 { margin: 12px 0; font-size: 26px; }
    p { color: var(--fm-muted); line-height: 1.6; overflow-wrap: anywhere; }
    button, a { display: inline-flex; min-height: 42px; align-items: center; padding: 10px 14px; border: 0; border-radius: 9px; background: var(--fm-primary); color: white; font: inherit; font-weight: 700; text-decoration: none; cursor: pointer; }
  `]
})
export class ConfirmEmailChange implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly accountSettings = inject(AccountSettingsService);

  readonly status = signal<ConfirmationState>('ready');
  readonly message = signal('');

  readonly email =
    this.route.snapshot.queryParamMap.get('email') ?? '';
  readonly newEmail =
    this.route.snapshot.queryParamMap.get('newEmail') ?? '';
  private readonly token =
    this.route.snapshot.queryParamMap.get('token') ?? '';

  ngOnInit(): void {
    if (!this.email || !this.newEmail || !this.token) {
      this.status.set('error');
      this.message.set(
        'This confirmation link is incomplete. Request a new email change from settings.'
      );
    }
  }

  confirm(): void {
    if (this.status() !== 'ready') return;

    this.status.set('loading');

    this.accountSettings
      .confirmEmailChange(this.email, this.newEmail, this.token)
      .subscribe({
        next: result => {
          this.status.set('success');
          this.message.set(result.message);
        },
        error: error => {
          this.status.set('error');
          this.message.set(
            error.status === 409
              ? 'That email address is already in use.'
              : 'This confirmation link is invalid or has expired.'
          );
        }
      });
  }
}
