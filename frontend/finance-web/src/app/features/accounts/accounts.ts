
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { FinancialAccount } from './financial-account.model';
import { AccountBalance } from '../dashboard/dashboard.model';

export interface CreateAccountRequest {
  name: string;
  accountType: string;
  openingBalance: number;
}

export interface UpdateAccountRequest {
  name: string;
  accountType: string;
  openingBalance: number;
}

@Injectable({
  providedIn: 'root'
})
export class Accounts {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/v1/accounts';

  getAccounts(): Observable<FinancialAccount[]> {
    return this.http.get<FinancialAccount[]>(this.apiUrl);
  }

  getBalances(): Observable<AccountBalance[]> {
    return this.http.get<AccountBalance[]>(
      '/api/v1/account-balances'
    );
  }

  createAccount(
    request: CreateAccountRequest
  ): Observable<FinancialAccount> {
    return this.http.post<FinancialAccount>(
      this.apiUrl,
      request
    );
  }

  updateAccount(
    id: string,
    request: UpdateAccountRequest
  ): Observable<FinancialAccount> {
    return this.http.put<FinancialAccount>(
      `${this.apiUrl}/${id}`,
      request
    );
  }

  deleteAccount(id: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/${id}`
    );
  }
}
