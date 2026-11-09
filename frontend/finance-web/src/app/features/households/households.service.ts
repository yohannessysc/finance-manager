import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

export type HouseholdRole = 'Owner' | 'Admin' | 'Editor' | 'Viewer';

export interface HouseholdMember {
  userId: string;
  email: string;
  firstName: string;
  lastName: string;
  role: HouseholdRole;
  joinedAtUtc: string;
}

export interface Household {
  id: string;
  name: string;
  ownerUserId: string;
  createdAtUtc: string;
  canManage: boolean;
  canEdit: boolean;
  canAssignAdmin: boolean;
  members: HouseholdMember[];
}

export interface HouseholdInvitation {
  id: string;
  email: string;
  role: HouseholdRole;
  expiresAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class HouseholdsService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/v1/households';

  getAll(): Observable<Household[]> {
    return this.http.get<Household[]>(this.url);
  }

  create(name: string): Observable<Household> {
    return this.http.post<Household>(this.url, { name });
  }

  invite(
    householdId: string,
    email: string,
    role: 'Admin' | 'Editor' | 'Viewer'
  ): Observable<HouseholdInvitation> {
    return this.http.post<HouseholdInvitation>(
      `${this.url}/${householdId}/invitations`,
      { email, role }
    );
  }

  accept(token: string): Observable<void> {
    return this.http.post<void>(
      `${this.url}/invitations/accept`,
      { token }
    );
  }

  updateRole(
    householdId: string,
    userId: string,
    role: 'Admin' | 'Editor' | 'Viewer'
  ): Observable<void> {
    return this.http.put<void>(
      `${this.url}/${householdId}/members/${userId}/role`,
      { role }
    );
  }

  removeMember(
    householdId: string,
    userId: string
  ): Observable<void> {
    return this.http.delete<void>(
      `${this.url}/${householdId}/members/${userId}`
    );
  }
}
