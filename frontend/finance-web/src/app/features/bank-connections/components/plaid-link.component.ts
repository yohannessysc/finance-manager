import {
  Component,
  OnDestroy,
  Output,
  EventEmitter,
  signal,
  effect,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { Subject, firstValueFrom } from 'rxjs';
import {
  BankConnectionService,
  BankConnectionResponse,
  ExchangePlaidTokenRequest
} from '../services/bank-connection.service';
import { AuthService } from '../../../core/auth/auth.service';

/**
 * Plaid Link component for connecting bank accounts
 * Flow: get link token -> open Plaid Link modal -> exchange public token on success
 */
@Component({
  selector: 'app-plaid-link',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="plaid-link">
      <div>
        <strong>Connect a bank</strong>
        <p>Sign in to your bank through Plaid. Finance Manager never sees your bank password.</p>
      </div>
      <button type="button" (click)="openPlaidLink()" [disabled]="isLoading()">
        {{ isLoading() ? 'Connecting...' : 'Connect bank' }}
      </button>
    </div>
    <p *ngIf="error()" class="message error" role="alert">{{ error() }}</p>
    <p *ngIf="successMessage()" class="message success" role="status">{{ successMessage() }}</p>
  `,
  styles: [`
    :host {
      display: block;
    }
    .plaid-link {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      padding: 20px;
      border: 1px solid var(--fm-border);
      border-radius: 14px;
      background: var(--fm-surface);
    }
    strong {
      font-size: 14px;
    }
    p {
      margin: 4px 0 0;
      color: var(--fm-muted);
      font-size: 12px;
    }
    button {
      min-height: 40px;
      padding: 10px 16px;
      border: 0;
      border-radius: 10px;
      background: var(--fm-primary);
      color: #fff;
      font: inherit;
      font-size: 13px;
      font-weight: 800;
      cursor: pointer;
    }
    button:disabled {
      opacity: .6;
      cursor: not-allowed;
    }
    .message {
      margin-top: 12px;
      padding: 10px 12px;
      border-radius: 10px;
      font-size: 13px;
    }
    .error {
      background: rgba(180, 35, 24, 0.1);
      color: #b42318;
    }
    .success {
      background: var(--fm-primary-soft);
      color: var(--fm-primary);
    }
  `]
})
export class PlaidLinkComponent implements OnDestroy {
  /** Emitted after a bank connection is saved, so the parent can refresh its list */
  @Output() connected = new EventEmitter<BankConnectionResponse>();

  isLoading = signal(false);
  error = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  private destroy$ = new Subject<void>();
  private handler: any = null;

  private bankConnectionService = inject(BankConnectionService);
  private authService = inject(AuthService);

  constructor() {
    effect((onCleanup) => {
      if (this.error()) {
        const t = setTimeout(() => this.error.set(null), 8000);
        onCleanup(() => clearTimeout(t));
      }
    });

    effect((onCleanup) => {
      if (this.successMessage()) {
        const t = setTimeout(() => this.successMessage.set(null), 10000);
        onCleanup(() => clearTimeout(t));
      }
    });
  }

  ngOnDestroy(): void {
    this.handler?.destroy?.();
    this.destroy$.next();
    this.destroy$.complete();
  }

  async openPlaidLink(): Promise<void> {
    this.error.set(null);
    this.successMessage.set(null);
    this.isLoading.set(true);

    try {
      const currentUser = this.authService.currentUser();
      if (!currentUser?.email) {
        throw new Error('User not authenticated');
      }

      // Load SDK and fetch link token in parallel
      const [Plaid, linkTokenResponse] = await Promise.all([
        loadPlaidSdk(),
        firstValueFrom(
          this.bankConnectionService.createLinkToken({
            userEmail: currentUser.email,
            userFullName: currentUser.email.split('@')[0]
          })
        )
      ]);

      if (!linkTokenResponse?.linkToken) {
        throw new Error('Failed to get link token');
      }

      this.handler?.destroy?.();
      this.handler = Plaid.create({
        token: linkTokenResponse.linkToken,
        onSuccess: (publicToken: string, metadata: any) =>
          this.handlePlaidSuccess(publicToken, metadata),
        onExit: (err: any) => {
          this.isLoading.set(false);
          if (err) {
            this.error.set(`Bank linking failed: ${err.display_message || err.error_message}`);
          }
        }
      });
      this.handler.open();
    } catch (err: any) {
      const msg =
        err?.error?.details || err?.error?.error || err?.message || 'Failed to initialize bank linking';
      console.error('openPlaidLink error:', err);
      this.error.set(msg);
      this.isLoading.set(false);
    }
  }

  private async handlePlaidSuccess(publicToken: string, metadata: any): Promise<void> {
    this.isLoading.set(true);
    const request: ExchangePlaidTokenRequest = {
      publicToken,
      metadata: {
        institutionId: metadata?.institution?.institution_id,
        institutionName: metadata?.institution?.name
      }
    };

    try {
      const connection = await firstValueFrom(
        this.bankConnectionService.exchangePublicToken(request)
      );

      // The backend runs a first sync on connect. Right after linking, Plaid may
      // still be pulling history, so retry a few times before giving up quietly.
      let imported = connection.transactionCount ?? 0;
      for (let attempt = 0; imported === 0 && attempt < 6; attempt++) {
        this.successMessage.set(`Connected to ${connection.institutionName}. Importing transactions…`);
        await new Promise(resolve => setTimeout(resolve, 5000));
        const sync = await firstValueFrom(this.bankConnectionService.syncConnection(connection.id));
        imported += sync.transactionsImported;
        // 'Pending' = Plaid still preparing history; anything else is final
        if (sync.status !== 'Pending') break;
      }

      this.successMessage.set(
        imported > 0
          ? `Connected to ${connection.institutionName}. Imported ${imported} transactions into your accounts.`
          : `Connected to ${connection.institutionName}. Transactions will appear shortly.`
      );
      this.connected.emit({ ...connection, transactionCount: imported });
    } catch (err: any) {
      console.error('Error exchanging token:', err);
      this.error.set(err?.error?.error || 'Failed to complete bank connection');
    } finally {
      this.isLoading.set(false);
    }
  }
}

const PLAID_SDK_URL = 'https://cdn.plaid.com/link/v2/stable/link-initialize.js';
let plaidSdkPromise: Promise<any> | null = null;

/** Loads Plaid Link SDK once and resolves with window.Plaid */
export function loadPlaidSdk(): Promise<any> {
  const w = window as any;
  if (w.Plaid) return Promise.resolve(w.Plaid);
  if (plaidSdkPromise) return plaidSdkPromise;

  plaidSdkPromise = new Promise((resolve, reject) => {
    const script = document.createElement('script');
    script.src = PLAID_SDK_URL;
    script.async = true;
    script.onload = () =>
      w.Plaid ? resolve(w.Plaid) : reject(new Error('Plaid SDK loaded but window.Plaid is missing'));
    script.onerror = () => {
      plaidSdkPromise = null;
      script.remove();
      reject(new Error('Could not load Plaid. Check your connection or disable ad/tracker blockers for this site.'));
    };
    document.head.appendChild(script);
  });
  return plaidSdkPromise;
}
