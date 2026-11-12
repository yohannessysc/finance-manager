import { Component, OnDestroy, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { BankConnectionService, BankConnectionResponse, BankSyncResponse } from '../services/bank-connection.service';
import { PlaidLinkComponent, loadPlaidSdk } from './plaid-link.component';
import { BillingService, SubscriptionPlan } from '../../../core/billing/billing.service';

@Component({
  selector: 'app-bank-connections',
  standalone: true,
  imports: [CommonModule, PlaidLinkComponent, RouterLink],
  template: `
    <section class="bank-page">
      <header class="page-heading">
        <span class="eyebrow">AUTOMATIC IMPORTS</span>
        <h1>Bank connections</h1>
        <p>Connect your bank to import accounts and transactions automatically.</p>
      </header>

      <!-- Linking needs the Bank plan; existing connections stay visible -->
      <app-plaid-link *ngIf="canLink() === true" (connected)="loadConnections()"></app-plaid-link>
      <div *ngIf="canLink() === false" class="upgrade-banner">
        <p>Automatic bank connections and syncing are part of the <strong>Bank</strong> plan.
          Connections you already have stay visible but stop syncing.</p>
        <a routerLink="/billing" class="primary-button">See plans</a>
      </div>

      <h2>Connected banks</h2>

      <p *ngIf="isLoading()" class="state-message">Loading connections...</p>

      <p *ngIf="connections().length === 0 && !isLoading()" class="state-message">
        No banks connected yet.
      </p>

      <div class="connections-grid">
        <article *ngFor="let connection of connections()" class="connection-card">
          <header>
            <h3>{{ connection.institutionName }}</h3>
            <span class="status-badge" [ngClass]="'status-' + connection.status.toLowerCase()">
              {{ statusLabel(connection.status) }}
            </span>
          </header>

          <dl>
            <div><dt>Connected</dt><dd>{{ connection.connectedAtUtc | date: 'mediumDate' }}</dd></div>
            <div><dt>Last sync</dt><dd>{{ connection.lastSyncAtUtc ? (connection.lastSyncAtUtc | date: 'short') : 'Not yet' }}</dd></div>
            <div><dt>Transactions</dt><dd>{{ connection.transactionCount }}</dd></div>
          </dl>

          <p *ngIf="connection.errorMessage" class="error-text">{{ connection.errorMessage }}</p>

          <p *ngIf="canLink() === false" class="sync-status">
            Syncing is paused on the Free plan. Transactions already imported stay in your accounts.
            <ng-container *ngIf="connection.removalScheduledAtUtc">
              This bank will be disconnected on {{ connection.removalScheduledAtUtc | date: 'mediumDate' }}
              unless you upgrade again.
            </ng-container>
          </p>

          <p *ngIf="syncStatus()[connection.id]" class="sync-status">{{ syncStatus()[connection.id] }}</p>

          <footer>
            <button
              *ngIf="connection.status === 'ExpiredLink' && canLink() !== false"
              type="button"
              class="primary-button"
              (click)="reconnect(connection)"
              [disabled]="syncing()[connection.id]">
              Reconnect
            </button>
            <button
              *ngIf="connection.status !== 'ExpiredLink' && canLink() !== false"
              type="button"
              class="outline-button"
              (click)="syncConnection(connection.id)"
              [disabled]="syncing()[connection.id]">
              {{ syncing()[connection.id] ? 'Syncing...' : 'Sync now' }}
            </button>
            <button type="button" class="text-button danger" (click)="disconnectConnection(connection.id)">
              Disconnect
            </button>
          </footer>
        </article>
      </div>
    </section>
  `,
  styles: [`
    :host {
      display: block;
      color: var(--fm-text);
    }
    .bank-page {
      max-width: 1100px;
      margin: 0 auto;
    }
    .page-heading {
      margin-bottom: 20px;
    }
    .page-heading h1 {
      margin: 8px 0;
      font-size: 32px;
    }
    .page-heading p,
    .state-message {
      margin: 0;
      color: var(--fm-muted);
    }
    .eyebrow {
      color: var(--fm-primary);
      font-size: 10px;
      font-weight: 800;
      letter-spacing: 1.5px;
    }
    h2 {
      margin: 28px 0 14px;
      font-size: 18px;
    }
    .upgrade-banner {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 14px;
      padding: 18px 20px;
      border: 1px solid var(--fm-border);
      border-radius: 14px;
      background: var(--fm-surface);
    }
    .upgrade-banner p {
      margin: 0;
      max-width: 620px;
    }
    .connections-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
      gap: 16px;
    }
    .connection-card {
      display: flex;
      flex-direction: column;
      gap: 14px;
      padding: 20px;
      border: 1px solid var(--fm-border);
      border-radius: 16px;
      background: var(--fm-surface);
      box-shadow: var(--fm-shadow);
    }
    .connection-card header,
    .connection-card footer {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 10px;
    }
    .connection-card footer {
      justify-content: flex-start;
      margin-top: auto;
    }
    .connection-card h3 {
      margin: 0;
      font-size: 16px;
    }
    dl {
      display: grid;
      gap: 8px;
      margin: 0;
    }
    dl div {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      font-size: 13px;
    }
    dt {
      color: var(--fm-muted);
    }
    dd {
      margin: 0;
      font-weight: 700;
    }
    .status-badge {
      padding: 5px 10px;
      border-radius: 999px;
      background: var(--fm-hover);
      color: var(--fm-muted);
      font-size: 11px;
      font-weight: 800;
    }
    .status-connected {
      background: var(--fm-primary-soft);
      color: var(--fm-primary);
    }
    .status-expiredlink,
    .status-failed {
      background: rgba(180, 35, 24, 0.12);
      color: #b42318;
    }
    .error-text {
      margin: 0;
      color: #b42318;
      font-size: 12px;
    }
    .sync-status {
      margin: 0;
      padding: 10px 12px;
      border-radius: 10px;
      background: var(--fm-background);
      color: var(--fm-muted);
      font-size: 12px;
    }
    .primary-button,
    .outline-button,
    .text-button {
      min-height: 38px;
      padding: 9px 14px;
      border-radius: 10px;
      font: inherit;
      font-size: 12px;
      font-weight: 800;
      text-decoration: none;
      cursor: pointer;
    }
    .primary-button {
      border: 0;
      background: var(--fm-primary);
      color: #fff;
    }
    .outline-button {
      border: 1px solid var(--fm-border);
      background: transparent;
      color: var(--fm-text);
    }
    .text-button {
      border: 0;
      background: transparent;
      color: var(--fm-primary);
    }
    .text-button.danger {
      color: #b42318;
    }
    button:disabled {
      opacity: .55;
      cursor: not-allowed;
    }
  `]
})
export class BankConnectionsComponent implements OnInit, OnDestroy {
  private readonly billingService = inject(BillingService);
  private reconnectHandler: any = null;

  // null until the plan is known, so neither the link button nor the
  // upgrade prompt flashes while loading.
  canLink = signal<boolean | null>(null);
  connections = signal<BankConnectionResponse[]>([]);
  isLoading = signal(true);
  syncing = signal<{ [key: string]: boolean }>({});
  syncStatus = signal<{ [key: string]: string }>({});

  constructor(private bankConnectionService: BankConnectionService) {}

  ngOnInit(): void {
    this.loadConnections();
    this.billingService.getStatus().subscribe({
      next: status => this.canLink.set(status.plan >= SubscriptionPlan.Bank),
      // The API still enforces the plan; fail open in the UI.
      error: () => this.canLink.set(true)
    });
  }

  ngOnDestroy(): void {
    this.reconnectHandler?.destroy?.();
  }

  statusLabel(status: string): string {
    switch (status) {
      case 'Connected': return 'Connected';
      case 'ExpiredLink': return 'Needs reconnect';
      case 'Disconnected': return 'Disconnected';
      case 'Failed': return 'Sync failed';
      default: return status;
    }
  }

  /**
   * Re-authenticate a connection whose bank login expired (Plaid update mode)
   */
  async reconnect(connection: BankConnectionResponse): Promise<void> {
    this.setSyncing(connection.id, true);
    this.setSyncStatus(connection.id, '');

    try {
      const [Plaid, token] = await Promise.all([
        loadPlaidSdk(),
        firstValueFrom(this.bankConnectionService.createReconnectToken(connection.id))
      ]);

      this.reconnectHandler?.destroy?.();
      this.reconnectHandler = Plaid.create({
        token: token.linkToken,
        onSuccess: async () => {
          try {
            const sync = await firstValueFrom(this.bankConnectionService.markReconnected(connection.id));
            this.setSyncStatus(connection.id, `Reconnected: ${sync.transactionsImported} new transactions imported`);
            this.loadConnections();
          } catch {
            this.setSyncStatus(connection.id, 'Reconnected at the bank, but the update failed. Try Sync now.');
          } finally {
            this.setSyncing(connection.id, false);
          }
        },
        onExit: (err: any) => {
          this.setSyncing(connection.id, false);
          if (err) {
            this.setSyncStatus(connection.id, `Reconnect failed: ${err.display_message || err.error_message}`);
          }
        }
      });
      this.reconnectHandler.open();
    } catch (err: any) {
      this.setSyncing(connection.id, false);
      this.setSyncStatus(connection.id, `${err?.error?.message || err?.error?.error || 'Could not start reconnecting.'}`);
    }
  }

  private setSyncing(connectionId: string, value: boolean): void {
    this.syncing.update(state => ({ ...state, [connectionId]: value }));
  }

  private setSyncStatus(connectionId: string, message: string): void {
    this.syncStatus.update(state => ({ ...state, [connectionId]: message }));
  }

  /**
   * Load all bank connections for the user
   */
  loadConnections(): void {
    this.isLoading.set(true);
    this.bankConnectionService.getUserConnections().subscribe({
      next: (connections) => {
        this.connections.set(connections);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Error loading connections:', err);
        this.isLoading.set(false);
      }
    });
  }

  /**
   * Trigger manual sync for a connection
   */
  syncConnection(connectionId: string): void {
    const syncing = this.syncing();
    syncing[connectionId] = true;
    this.syncing.set(syncing);

    this.bankConnectionService.syncConnection(connectionId).subscribe({
      next: (response: BankSyncResponse) => {
        const status = this.syncStatus();
        status[connectionId] = `Sync complete: ${response.transactionsImported} imported, ${response.duplicatesSkipped} duplicates skipped`;
        this.syncStatus.set(status);

        const syncingState = this.syncing();
        syncingState[connectionId] = false;
        this.syncing.set(syncingState);

        // Reload connections to update counts
        setTimeout(() => this.loadConnections(), 1000);
      },
      error: (err) => {
        console.error('Error syncing connection:', err);
        const status = this.syncStatus();
        status[connectionId] = `Sync failed: ${err.error?.error || 'Unknown error'}`;
        this.syncStatus.set(status);

        const syncingState = this.syncing();
        syncingState[connectionId] = false;
        this.syncing.set(syncingState);
      }
    });
  }

  /**
   * Disconnect a bank connection
   */
  disconnectConnection(connectionId: string): void {
    if (!confirm('Are you sure you want to disconnect this bank account?')) {
      return;
    }

    this.bankConnectionService.disconnectConnection(connectionId).subscribe({
      next: () => {
        this.loadConnections();
      },
      error: (err) => {
        console.error('Error disconnecting:', err);
        alert('Failed to disconnect bank account');
      }
    });
  }
}
