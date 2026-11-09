
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { CashFlowForecast, DashboardSummary } from './dashboard.model';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = '/api/v1/dashboard/summary';

  getSummary(): Observable<DashboardSummary> {
    return this.http.get<DashboardSummary>(this.apiUrl);
  }

  getForecast(days = 30): Observable<CashFlowForecast> {
    return this.http.get<CashFlowForecast>(
      `/api/v1/dashboard/forecast?days=${days}`
    );
  }
}
