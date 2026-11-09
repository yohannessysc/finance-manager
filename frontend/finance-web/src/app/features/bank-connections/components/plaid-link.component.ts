import {
  Component,
  OnInit,
  OnDestroy,
  ViewChild,
  ElementRef,
  signal,
  effect
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { Subject, takeUntil } from 'rxjs';
import {
  BankConnectionService,
  PlaidLinkTokenResponse,
  BankConnectionResponse,
  ExchangePlaidTokenRequest
} from '../services/bank-connection.service';

/**
 * Plaid Link component for connecting bank accounts
 * Handles the complete flow: get link token → open Plaid Link modal → exchange token
 */
@Component({
  selector: 'app-plaid-link',
  standalone: true,
  imports: [CommonModule, HttpClientModule],
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

      <!-- Hidden div where Plaid Link will render -->
      <div #plaidLinkContainer></div>
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
export class PlaidLinkComponent implements OnInit, OnDestroy {
  @ViewChild('plaidLinkContainer', { static: false }) plaidLinkContainer?: ElementRef;

  // Signal state management
  isLoading = signal(false);
  error = signal<string | null>(null);
  successMessage = signal<string | null>(null);

  private destroy$ = new Subject<void>();
  private linkToken: string | null = null;
  private plaidScriptLoaded = false;
  private plaidApiHost = 'https://cdn.plaid.com/link/v3/stable/plain.js';

  constructor(private bankConnectionService: BankConnectionService) {
    // Auto-clear error after 5 seconds
    effect(() => {
      const errorMsg = this.error();
      if (errorMsg) {
        const timeout = setTimeout(() => this.error.set(null), 5000);
        return () => clearTimeout(timeout);
      }
      return undefined;
    });

    // Auto-clear success message after 3 seconds
    effect(() => {
      const successMsg = this.successMessage();
      if (successMsg) {
        const timeout = setTimeout(() => this.successMessage.set(null), 3000);
        return () => clearTimeout(timeout);
      }
      return undefined;
    });
  }

  ngOnInit(): void {
    this.loadPlaidScript();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Load Plaid SDK script from CDN
   */
  private loadPlaidScript(): void {
    if (this.plaidScriptLoaded) return;

    const script = document.createElement('script');
    script.src = this.plaidApiHost;
    script.async = true;
    script.onload = () => {
      this.plaidScriptLoaded = true;
    };
    script.onerror = () => {
      this.error.set('Failed to load Plaid. Please try again.');
      this.plaidScriptLoaded = false;
    };
    document.body.appendChild(script);
  }

  /**
   * Open Plaid Link modal
   */
  async openPlaidLink(): Promise<void> {
    try {
      this.error.set(null);
      this.successMessage.set(null);
      this.isLoading.set(true);

      // Step 1: Get link token from backend
      const linkTokenResponse = await this.bankConnectionService
        .createLinkToken({
          userEmail: 'user@example.com',
          userFullName: 'User Name'
        })
        .toPromise();

      if (!linkTokenResponse?.linkToken) {
        throw new Error('Failed to get link token');
      }

      this.linkToken = linkTokenResponse.linkToken;
      this.isLoading.set(false);

      // Step 2: Open Plaid Link with the token
      this.openPlaidLinkModal();
    } catch (err) {
      this.error.set(
        err instanceof Error ? err.message : 'Failed to initialize bank linking'
      );
      this.isLoading.set(false);
    }
  }

  /**
   * Open Plaid Link modal using Plaid SDK
   */
  private openPlaidLinkModal(): void {
    // Check if Plaid is loaded
    const plaid = (window as any).Plaid;
    if (!plaid) {
      this.error.set('Plaid SDK not loaded. Please refresh and try again.');
      return;
    }

    // Create Plaid Link handler
    const handler = plaid.create({
      token: this.linkToken,
      onSuccess: (publicToken: string, metadata: any) => {
        this.handlePlaidSuccess(publicToken, metadata);
      },
      onExit: (err: any, metadata: any) => {
        this.handlePlaidExit(err, metadata);
      },
      onEvent: (eventName: string, metadata: any) => {
        console.log('Plaid event:', eventName, metadata);
      }
    });

    // Open the Link modal
    handler.open();
  }

  /**
   * Handle successful Plaid Link completion
   */
  private handlePlaidSuccess(publicToken: string, metadata: any): void {
    this.isLoading.set(true);
    this.error.set(null);

    try {
      const exchangeRequest: ExchangePlaidTokenRequest = {
        publicToken,
        metadata: {
          institutionId: metadata.institution?.institution_id,
          institutionName: metadata.institution?.name
        },
        financialAccountId: undefined
      };

      // Step 3: Exchange public token for access token
      this.bankConnectionService
        .exchangePublicToken(exchangeRequest)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: (connection: BankConnectionResponse) => {
            this.isLoading.set(false);
            this.successMessage.set(
              `✅ Successfully connected to ${connection.institutionName}!`
            );
            console.log('Bank connection established:', connection);
            // You can emit an event here to refresh connections list
          },
          error: (err) => {
            this.isLoading.set(false);
            this.error.set(
              err.error?.error || 'Failed to complete bank connection'
            );
            console.error('Error exchanging token:', err);
          }
        });
    } catch (err) {
      this.isLoading.set(false);
      this.error.set('Error processing bank connection');
      console.error('Error:', err);
    }
  }

  /**
   * Handle Plaid Link exit (user cancelled or error)
   */
  private handlePlaidExit(err: any, metadata: any): void {
    if (err) {
      this.error.set(`Bank linking cancelled: ${err.error_message}`);
    }
    this.isLoading.set(false);
  }
}
