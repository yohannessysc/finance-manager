import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';

import { HouseholdsService } from './households.service';

@Component({
  selector: 'app-accept-household-invitation',
  standalone: true,
  imports: [RouterLink],
  template: `
    <main class="invitation-page">
      <h1>Household invitation</h1>
      @if (loading()) {
        <p>Checking your invitation...</p>
      } @else if (error()) {
        <p role="alert">{{ error() }}</p>
      } @else {
        <p role="status">You joined the household successfully.</p>
        <a routerLink="/households">Manage households</a>
      }
    </main>
  `,
  styles: [`
    .invitation-page { max-width: 520px; margin: 12vh auto; padding: 28px; }
    p { line-height: 1.6; }
    a { color: var(--fm-primary); }
  `]
})
export class AcceptHouseholdInvitation implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly households = inject(HouseholdsService);

  readonly loading = signal(true);
  readonly error = signal('');

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (!token) {
      this.error.set('The invitation link is missing its token.');
      this.loading.set(false);
      return;
    }

    this.households.accept(token).subscribe({
      next: () => this.loading.set(false),
      error: error => {
        this.error.set(
          error.error?.message ??
          'This invitation is invalid, expired, or for another account.'
        );
        this.loading.set(false);
      }
    });
  }
}
