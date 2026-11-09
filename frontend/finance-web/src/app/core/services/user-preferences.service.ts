
import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface UserPreferences {
  defaultCurrency: string;
  timeZone: string;
  dateFormat: string;
  budgetStartDay: number;
  updatedAtUtc: string;
}

export interface UpdateUserPreferencesRequest {
  defaultCurrency: string;
  timeZone: string;
  dateFormat: string;
  budgetStartDay: number;
}

@Injectable({
  providedIn: 'root'
})
export class UserPreferencesService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/user-preferences';

  get(): Observable<UserPreferences> {
    return this.http.get<UserPreferences>(this.url);
  }

  update(
    request: UpdateUserPreferencesRequest
  ): Observable<UserPreferences> {
    return this.http.put<UserPreferences>(
      this.url,
      request
    );
  }
}
