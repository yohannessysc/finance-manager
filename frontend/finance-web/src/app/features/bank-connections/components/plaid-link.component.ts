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
    <div class="plaid-link-container">
      <button 
        (click)="openPlaidLink()" 
        [disabled]="isLoading()"
        class="connect-bank-btn">
        {{ isLoading() ? 'Loading...' : '🏦 Connect Bank Account' }}
      </button>
      
      <div *ngIf="error()" class="error-message">
        {{ error() }}
      </div>
      
      <div *ngIf="successMessage()" class="success-message">
        {{ successMessage() }}
      </div>
    </div>
  `,
  styles: [`
    .plaid-link-container {
      padding: 1.5rem;
      border: 1px solid #e0e0e0;
      border-radius: 8px;
      max-width: 400px;
      margin: 1rem 0;
    }

    .connect-bank-btn {
      width: 100%;
      padding: 0.75rem 1rem;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      border: none;
      border-radius: 6px;
      font-size: 1rem;
      font-weight: 600;
      cursor: pointer;
      transition: transform 0.2s, box-shadow 0.2s;
    }

    .connect-bank-btn:hover:not(:disabled) {
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(102, 126, 234, 0.4);
    }

    .connect-bank-btn:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .error-message {
      margin-top: 1rem;
      padding: 0.75rem;
      background-color: #fee;
      color: #c33;
      border-left: 4px solid #c33;
      border-radius: 4px;
      font-size: 0.9rem;
    }

    .success-message {
      margin-top: 1rem;
      padding: 0.75rem;
      background-color: #efe;
      color: #3c3;
      border-left: 4px solid #3c3;
      border-radius: 4px;
      font-size: 0.9rem;
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
          ? `✅ Connected to ${connection.institutionName} — imported ${imported} transactions into your accounts.`
          : `✅ Connected to ${connection.institutionName}. Transactions will appear shortly.`
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
