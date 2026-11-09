
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface AccountProfile {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  emailConfirmed: boolean;
  createdAtUtc: string;
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface RequestEmailChangeRequest {
  newEmail: string;
  currentPassword: string;
}

export interface MessageResponse {
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class AccountSettingsService {
  private readonly http = inject(HttpClient);

  private readonly url = '/api/v1/account-settings';

  getProfile(): Observable<AccountProfile> {
    return this.http.get<AccountProfile>(
      `${this.url}/profile`
    );
  }

  updateProfile(
    request: UpdateProfileRequest
  ): Observable<AccountProfile> {
    return this.http.put<AccountProfile>(
      `${this.url}/profile`,
      request
    );
  }

  changePassword(
    request: ChangePasswordRequest
  ): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(
      `${this.url}/change-password`,
      request
    );
  }

  requestEmailChange(
    request: RequestEmailChangeRequest
  ): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(
      `${this.url}/change-email`,
      request
    );
  }

  confirmEmailChange(
    email: string,
    newEmail: string,
    token: string
  ): Observable<MessageResponse> {
    return this.http.post<MessageResponse>(
      `${this.url}/confirm-email-change`,
      { email, newEmail, token }
    );
  }
}
