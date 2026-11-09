import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface UserSession {
  id: string;
  deviceName: string;
  createdAtUtc: string;
  lastSeenAtUtc: string;
  expiresAtUtc: string;
  isCurrent: boolean;
}

export interface RevokedResponse {
  revoked: number;
}

@Injectable({
  providedIn: 'root'
})
export class SessionsService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/sessions';

  list(): Observable<UserSession[]> {
    return this.http.get<UserSession[]>(this.url);
  }

  revoke(id: string): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }

  revokeOthers(): Observable<RevokedResponse> {
    return this.http.post<RevokedResponse>(
      `${this.url}/revoke-others`,
      {}
    );
  }

  revokeAll(): Observable<RevokedResponse> {
    return this.http.post<RevokedResponse>(
      `${this.url}/revoke-all`,
      {}
    );
  }
}
