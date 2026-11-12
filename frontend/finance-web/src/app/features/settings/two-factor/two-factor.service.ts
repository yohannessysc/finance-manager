import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export interface TwoFactorStatus {
  enabled: boolean;
  recoveryCodesLeft: number;
}

export interface TwoFactorSetup {
  sharedKey: string;
  authenticatorUri: string;
}

@Injectable({
  providedIn: 'root'
})
export class TwoFactorService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/account/two-factor';

  getStatus(): Observable<TwoFactorStatus> {
    return this.http.get<TwoFactorStatus>(this.url);
  }

  setup(): Observable<TwoFactorSetup> {
    return this.http.post<TwoFactorSetup>(`${this.url}/setup`, {});
  }

  enable(code: string): Observable<{ recoveryCodes: string[] }> {
    return this.http.post<{ recoveryCodes: string[] }>(`${this.url}/enable`, { code });
  }

  disable(password: string): Observable<void> {
    return this.http.post<void>(`${this.url}/disable`, { password });
  }

  regenerateRecoveryCodes(password: string): Observable<{ recoveryCodes: string[] }> {
    return this.http.post<{ recoveryCodes: string[] }>(`${this.url}/recovery-codes`, { password });
  }
}
