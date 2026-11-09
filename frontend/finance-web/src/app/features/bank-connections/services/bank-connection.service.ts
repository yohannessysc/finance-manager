import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface CreatePlaidLinkRequest {
  userEmail?: string;
  userFullName?: string;
}

export interface ExchangePlaidTokenRequest {
  publicToken: string;
  metadata?: {
    institutionId?: string;
    institutionName?: string;
  };
  financialAccountId?: string;
}

export interface PlaidLinkTokenResponse {
  linkToken: string;
  expiresAtUtc: string;
  requestId: string;
}

export interface BankConnectionResponse {
  id: string;
  institutionName: string;
  bankProvider: string;
  status: string;
  connectedAtUtc: string;
  lastSyncAtUtc?: string;
  expiresAtUtc: string;
  errorMessage?: string;
  transactionCount: number;
  financialAccountId?: string;
}

export interface BankSyncResponse {
  syncId: string;
  status: string;
  transactionsRetrieved: number;
  transactionsImported: number;
  duplicatesSkipped: number;
  errorCount: number;
  completedAt: string;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class BankConnectionService {
  private readonly apiUrl = '/api/v1/bank';

  constructor(private http: HttpClient) {}

  /**
   * Get Plaid Link token to initiate bank account linking
   */
  createLinkToken(request: CreatePlaidLinkRequest): Observable<PlaidLinkTokenResponse> {
    return this.http.post<PlaidLinkTokenResponse>(
      `${this.apiUrl}/plaid/link-token`,
      request
    );
  }

  /**
   * Exchange Plaid public token for access token
   */
  exchangePublicToken(request: ExchangePlaidTokenRequest): Observable<BankConnectionResponse> {
    return this.http.post<BankConnectionResponse>(
      `${this.apiUrl}/plaid/exchange-token`,
      request
    );
  }

  /**
   * Get all bank connections for the user
   */
  getUserConnections(): Observable<BankConnectionResponse[]> {
    return this.http.get<BankConnectionResponse[]>(
      `${this.apiUrl}/connections`
    );
  }

  /**
   * Get a specific bank connection
   */
  getConnection(connectionId: string): Observable<BankConnectionResponse> {
    return this.http.get<BankConnectionResponse>(
      `${this.apiUrl}/connections/${connectionId}`
    );
  }

  /**
   * Disconnect a bank connection
   */
  disconnectConnection(connectionId: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/connections/${connectionId}`
    );
  }

  /**
   * Get transactions for a connection
   */
  getConnectionTransactions(
    connectionId: string,
    pageNumber: number = 1,
    pageSize: number = 50
  ): Observable<any> {
    return this.http.get(
      `${this.apiUrl}/connections/${connectionId}/transactions`,
      {
        params: {
          pageNumber: pageNumber.toString(),
          pageSize: pageSize.toString()
        }
      }
    );
  }

  /**
   * Manually trigger a sync for a connection
   */
  syncConnection(connectionId: string): Observable<BankSyncResponse> {
    return this.http.post<BankSyncResponse>(
      `${this.apiUrl}/connections/${connectionId}/sync`,
      {}
    );
  }
}
