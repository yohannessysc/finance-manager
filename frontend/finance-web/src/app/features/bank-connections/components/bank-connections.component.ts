import { Component, OnInit, signal, effect } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClientModule } from '@angular/common/http';
import { BankConnectionService, BankConnectionResponse, BankSyncResponse } from '../services/bank-connection.service';
import { PlaidLinkComponent } from './plaid-link.component';

@Component({
  selector: 'app-bank-connections',
  standalone: true,
  imports: [CommonModule, HttpClientModule, PlaidLinkComponent],
  template: `
    <div class="bank-connections-container">
      <h1>🏦 Bank Connections</h1>
      <p class="description">Connect your bank accounts to automatically import transactions</p>

      <!-- Plaid Link Component -->
      <app-plaid-link></app-plaid-link>

      <!-- Connections List -->
      <div class="connections-section">
        <h2>Connected Banks</h2>
        
        <div *ngIf="isLoading()" class="loading">
          Loading connections...
        </div>

        <div *ngIf="connections().length === 0 && !isLoading()" class="empty-state">
          <p>📭 No connected banks yet. Use the form above to connect your first bank account.</p>
        </div>

        <div class="connections-grid">
          <div *ngFor="let connection of connections()" class="connection-card">
            <div class="card-header">
              <h3>{{ connection.institutionName }}</h3>
              <span [ngClass]="'status-badge status-' + connection.status.toLowerCase()">
                {{ connection.status }}
              </span>
            </div>

            <div class="card-body">
              <p><strong>Provider:</strong> {{ connection.bankProvider }}</p>
              <p><strong>Status:</strong> {{ connection.status }}</p>
              <p><strong>Connected:</strong> {{ connection.connectedAtUtc | date: 'short' }}</p>
              <p *ngIf="connection.lastSyncAtUtc">
                <strong>Last Sync:</strong> {{ connection.lastSyncAtUtc | date: 'short' }}
              </p>
              <p><strong>Transactions:</strong> {{ connection.transactionCount }}</p>
              <p *ngIf="connection.expiresAtUtc">
                <strong>Expires:</strong> {{ connection.expiresAtUtc | date: 'short' }}
              </p>
              <p *ngIf="connection.errorMessage" class="error-text">
                <strong>Error:</strong> {{ connection.errorMessage }}
              </p>
            </div>

            <div class="card-actions">
              <button 
                (click)="syncConnection(connection.id)"
                [disabled]="syncing()[connection.id]"
                class="btn btn-sync">
                {{ syncing()[connection.id] ? '⏳ Syncing...' : '🔄 Sync Now' }}
              </button>
              <button 
                (click)="disconnectConnection(connection.id)"
                class="btn btn-disconnect">
                🔌 Disconnect
              </button>
            </div>

            <div *ngIf="syncStatus()[connection.id]" class="sync-status">
              <p class="sync-info">{{ syncStatus()[connection.id] }}</p>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .bank-connections-container {
      padding: 2rem;
      max-width: 1200px;
      margin: 0 auto;
    }

    h1 {
      font-size: 2rem;
      color: #333;
      margin-bottom: 0.5rem;
    }

    .description {
      color: #666;
      margin-bottom: 2rem;
      font-size: 1.1rem;
    }

    .connections-section {
      margin-top: 3rem;
    }

    h2 {
      font-size: 1.5rem;
      color: #333;
      margin-bottom: 1.5rem;
      border-bottom: 2px solid #667eea;
      padding-bottom: 0.5rem;
    }

    .loading {
      text-align: center;
      padding: 2rem;
      color: #666;
      font-size: 1.1rem;
    }

    .empty-state {
      text-align: center;
      padding: 2rem;
      background: #f5f5f5;
      border-radius: 8px;
      color: #666;
    }

    .connections-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(350px, 1fr));
      gap: 1.5rem;
    }

    .connection-card {
      border: 1px solid #e0e0e0;
      border-radius: 8px;
      box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
      overflow: hidden;
      transition: transform 0.2s, box-shadow 0.2s;
    }

    .connection-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
    }

    .card-header {
      padding: 1rem;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .card-header h3 {
      margin: 0;
      font-size: 1.2rem;
    }

    .status-badge {
      padding: 0.25rem 0.75rem;
      border-radius: 20px;
      font-size: 0.85rem;
      font-weight: 600;
    }

    .status-connected {
      background: rgba(255, 255, 255, 0.3);
    }

    .status-pending {
      background: rgba(255, 255, 100, 0.3);
    }

    .status-disconnected,
    .status-failed {
      background: rgba(255, 100, 100, 0.3);
    }

    .card-body {
      padding: 1rem;
      background: white;
    }

    .card-body p {
      margin: 0.5rem 0;
      font-size: 0.95rem;
      color: #333;
    }

    .error-text {
      color: #c33;
      font-weight: 600;
    }

    .card-actions {
      padding: 1rem;
      background: #f9f9f9;
      border-top: 1px solid #e0e0e0;
      display: flex;
      gap: 0.5rem;
    }

    .btn {
      flex: 1;
      padding: 0.75rem;
      border: none;
      border-radius: 6px;
      font-size: 0.9rem;
      font-weight: 600;
      cursor: pointer;
      transition: background 0.2s;
    }

    .btn-sync {
      background: #667eea;
      color: white;
    }

    .btn-sync:hover:not(:disabled) {
      background: #5568d3;
    }

    .btn-sync:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .btn-disconnect {
      background: #f0f0f0;
      color: #333;
    }

    .btn-disconnect:hover {
      background: #e0e0e0;
    }

    .sync-status {
      padding: 0.75rem 1rem;
      background: #efe;
      border-top: 1px solid #cfc;
      font-size: 0.9rem;
      color: #3c3;
    }

    .sync-info {
      margin: 0;
    }
  `]
})
export class BankConnectionsComponent implements OnInit {
  connections = signal<BankConnectionResponse[]>([]);
  isLoading = signal(true);
  syncing = signal<{ [key: string]: boolean }>({});
  syncStatus = signal<{ [key: string]: string }>({});

  constructor(private bankConnectionService: BankConnectionService) {}

  ngOnInit(): void {
    this.loadConnections();
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
        status[connectionId] = `✅ Sync complete: ${response.transactionsImported} imported, ${response.duplicatesSkipped} duplicates skipped`;
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
        status[connectionId] = `❌ Sync failed: ${err.error?.error || 'Unknown error'}`;
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
