
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { FinancialAccount } from './financial-account.model';

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

  // GET - Retrieve accounts
  getAccounts(): Observable<FinancialAccount[]> {
    return this.http.get<FinancialAccount[]>(this.apiUrl);
  }

  // POST - Create account
  createAccount(
    request: CreateAccountRequest
  ): Observable<FinancialAccount> {
    return this.http.post<FinancialAccount>(
      this.apiUrl,
      request
    );
  }

  // PUT - Update account
  updateAccount(
    id: string,
    request: UpdateAccountRequest
  ): Observable<FinancialAccount> {
    return this.http.put<FinancialAccount>(
      `${this.apiUrl}/${id}`,
      request
    );
  }

  // DELETE - Delete account
  deleteAccount(id: string): Observable<void> {
    return this.http.delete<void>(
      `${this.apiUrl}/${id}`
    );
  }
}
