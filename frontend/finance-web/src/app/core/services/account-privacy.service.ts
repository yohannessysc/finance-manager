import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { MessageResponse } from './account-settings.service';

export interface DeleteAccountRequest {
  currentPassword: string;
  confirmation: string;
}

@Injectable({
  providedIn: 'root'
})
export class AccountPrivacyService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/account-privacy';

  exportFinancialData(): Observable<Blob> {
    return this.http.get(`${this.url}/export`, {
      responseType: 'blob'
    });
  }

  deleteAccount(request: DeleteAccountRequest): Observable<void> {
    return this.http.post<void>(
      `${this.url}/delete-account`,
      request
    );
  }
}
