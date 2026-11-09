
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { signal } from '@angular/core';
import { of } from 'rxjs';
import { vi } from 'vitest';

import { Settings } from './settings';
import { AuthService } from '../../core/auth/auth.service';

import {
  AccountSettingsService,
  AccountProfile
} from '../../core/services/account-settings.service';

import {
  ACCENT_OPTIONS,
  ThemeService,
  ThemePreference,
  AccentColor
} from '../../core/services/theme.service';

import {
  UserPreferences,
  UserPreferencesService
} from '../../core/services/user-preferences.service';

import { AccountPrivacyService } from '../../core/services/account-privacy.service';

import {
  SessionsService,
  UserSession
} from '../../core/services/sessions.service';

describe('Settings', () => {
  const sessionList: UserSession[] = [
    {
      id: 'current',
      deviceName: 'Mac · Chrome',
      createdAtUtc: '2026-11-01T18:00:00Z',
      lastSeenAtUtc: '2026-11-01T18:10:00Z',
      expiresAtUtc: '2026-11-01T18:30:00Z',
      isCurrent: true
    },
    {
      id: 'other',
      deviceName: 'iPhone · Safari',
      createdAtUtc: '2026-11-01T17:00:00Z',
      lastSeenAtUtc: '2026-11-01T17:30:00Z',
      expiresAtUtc: '2026-11-01T18:30:00Z',
      isCurrent: false
    }
  ];

  let mockSessions: {
    list: ReturnType<typeof vi.fn>;
    revoke: ReturnType<typeof vi.fn>;
    revokeOthers: ReturnType<typeof vi.fn>;
    revokeAll: ReturnType<typeof vi.fn>;
  };

  const savedPreferences: UserPreferences = {
    defaultCurrency: 'CAD',
    timeZone: 'America/Toronto',
    dateFormat: 'yyyy-MM-dd',
    budgetStartDay: 1,
    updatedAtUtc: '2026-10-25T20:00:00Z'
  };

  let mockPreferences: {
    get: ReturnType<typeof vi.fn>;
    update: ReturnType<typeof vi.fn>;
  };

  const profile: AccountProfile = {
    id: '11111111-1111-4111-8111-111111111111',
    firstName: 'Test',
    lastName: 'User',
    email: 'test@example.com',
    emailConfirmed: true,
    createdAtUtc: '2026-10-23T12:00:00Z'
  };

  let mockAccount: {
    getProfile: ReturnType<typeof vi.fn>;
    updateProfile: ReturnType<typeof vi.fn>;
    changePassword: ReturnType<typeof vi.fn>;
    requestEmailChange: ReturnType<typeof vi.fn>;
    confirmEmailChange: ReturnType<typeof vi.fn>;
  };

  let mockPrivacy: {
    exportFinancialData: ReturnType<typeof vi.fn>;
    deleteAccount: ReturnType<typeof vi.fn>;
  };

  beforeEach(async () => {
    mockAccount = {
      getProfile: vi.fn().mockReturnValue(of(profile)),
      updateProfile: vi.fn().mockReturnValue(of(profile)),
      changePassword: vi.fn().mockReturnValue(
        of({ message: 'Password changed.' })
      ),
      requestEmailChange: vi.fn().mockReturnValue(
        of({ message: 'Confirmation sent.' })
      ),
      confirmEmailChange: vi.fn().mockReturnValue(
        of({ message: 'Email updated.' })
      )
    };

    mockPrivacy = {
      exportFinancialData: vi.fn().mockReturnValue(
        of(new Blob(['{}'], { type: 'application/json' }))
      ),
      deleteAccount: vi.fn().mockReturnValue(of(undefined))
    };

    mockSessions = {
      list: vi.fn().mockReturnValue(of(sessionList)),
      revoke: vi.fn().mockReturnValue(of(undefined)),
      revokeOthers: vi.fn().mockReturnValue(of({ revoked: 1 })),
      revokeAll: vi.fn().mockReturnValue(of({ revoked: 2 }))
    };

    mockPreferences = {
      get: vi.fn().mockReturnValue(of(savedPreferences)),
      update: vi.fn().mockReturnValue(of(savedPreferences))
    };

    const preference = signal<ThemePreference>('system');
    const accent = signal<AccentColor>('emerald');

    await TestBed.configureTestingModule({
      imports: [Settings],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: {
            currentUser: signal({
              id: profile.id,
              firstName: profile.firstName,
              lastName: profile.lastName,
              email: profile.email
            })
          }
        },
        {
          provide: AccountSettingsService,
          useValue: mockAccount
        },
        {
          provide: AccountPrivacyService,
          useValue: mockPrivacy
        },
        {
          provide: SessionsService,
          useValue: mockSessions
        },
        {
          provide: UserPreferencesService,
          useValue: mockPreferences
        },
        {
          provide: ThemeService,
          useValue: {
            preference,
            accent,
            accents: ACCENT_OPTIONS,
            resolvedTheme: signal('light'),
            setTheme: (value: ThemePreference) =>
              preference.set(value),
            setAccent: (value: AccentColor) =>
              accent.set(value)
          }
        }
      ]
    }).compileComponents();
  });

  it('loads profile from the account API', () => {
    const fixture = TestBed.createComponent(Settings);

    fixture.detectChanges();

    expect(mockAccount.getProfile).toHaveBeenCalledOnce();
    expect(fixture.componentInstance.profile()?.email)
      .toBe('test@example.com');
  });

  it('loads financial preferences into the form', () => {
    mockPreferences.get.mockReturnValue(of({
      ...savedPreferences,
      defaultCurrency: 'USD',
      budgetStartDay: 15
    }));

    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;

    expect(mockPreferences.get).toHaveBeenCalledOnce();
    expect(component.preferencesForm.getRawValue().defaultCurrency)
      .toBe('USD');
    expect(component.preferencesForm.getRawValue().budgetStartDay)
      .toBe(15);
  });

  it('saves financial preferences', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;

    component.preferencesForm.patchValue({ budgetStartDay: 15 });
    component.savePreferences();

    expect(mockPreferences.update).toHaveBeenCalledWith({
      defaultCurrency: 'CAD',
      timeZone: 'America/Toronto',
      dateFormat: 'yyyy-MM-dd',
      budgetStartDay: 15
    });
    expect(component.preferencesMessage())
      .toBe('Your financial preferences have been saved.');
  });

  it('rejects an out-of-range budget start day', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;

    component.preferencesForm.patchValue({ budgetStartDay: 29 });
    component.savePreferences();

    expect(component.preferencesForm.invalid).toBe(true);
    expect(mockPreferences.update).not.toHaveBeenCalled();
  });

  it('loads active sessions', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    expect(mockSessions.list).toHaveBeenCalledOnce();
    expect(fixture.componentInstance.sessions().length).toBe(2);
    expect(fixture.componentInstance.hasOtherSessions()).toBe(true);
  });

  it('revokes another session and removes it from the list', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.revokeSession(sessionList[1]);

    expect(mockSessions.revoke).toHaveBeenCalledWith('other');
    expect(component.sessions().map(x => x.id)).toEqual(['current']);
  });

  it('does not revoke the current session from the list', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    fixture.componentInstance.revokeSession(sessionList[0]);

    expect(mockSessions.revoke).not.toHaveBeenCalled();
  });

  it('signs out other devices and keeps the current one', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.signOutOtherSessions();

    expect(mockSessions.revokeOthers).toHaveBeenCalledOnce();
    expect(component.sessions().map(x => x.id)).toEqual(['current']);
  });

  it('signs out everywhere', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const navigate = vi
      .spyOn(TestBed.inject(Router), 'navigate')
      .mockResolvedValue(true);

    fixture.componentInstance.signOutEverywhere();

    expect(mockSessions.revokeAll).toHaveBeenCalledOnce();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('validates empty profile names', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;

    component.profileForm.controls.firstName.setValue('');
    component.saveProfile();

    expect(component.profileForm.invalid).toBe(true);
    expect(mockAccount.updateProfile).not.toHaveBeenCalled();
  });

  it('saves updated profile names', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;

    component.profileForm.setValue({
      firstName: 'Updated',
      lastName: 'Name'
    });

    component.saveProfile();

    expect(mockAccount.updateProfile).toHaveBeenCalledWith({
      firstName: 'Updated',
      lastName: 'Name'
    });
  });

  it('requests an email change only with a valid email and password', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;

    component.emailChangeForm.setValue({
      newEmail: 'new@example.com',
      currentPassword: 'CurrentPassword123!'
    });
    component.requestEmailChange();

    expect(mockAccount.requestEmailChange).toHaveBeenCalledWith({
      newEmail: 'new@example.com',
      currentPassword: 'CurrentPassword123!'
    });
    expect(component.emailChangeMessage()).toBe('Confirmation sent.');
  });

  it('requires the exact deletion confirmation phrase', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;

    component.deleteAccountForm.setValue({
      currentPassword: 'CurrentPassword123!',
      confirmation: 'delete'
    });
    component.deleteAccount();

    expect(mockPrivacy.deleteAccount).not.toHaveBeenCalled();
    expect(component.privacyError()).toContain('Type DELETE');
  });

  it('deletes the account and returns to sign in', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const navigate = vi
      .spyOn(TestBed.inject(Router), 'navigate')
      .mockResolvedValue(true);

    fixture.componentInstance.deleteAccountForm.setValue({
      currentPassword: 'CurrentPassword123!',
      confirmation: 'DELETE'
    });
    fixture.componentInstance.deleteAccount();

    expect(mockPrivacy.deleteAccount).toHaveBeenCalledWith({
      currentPassword: 'CurrentPassword123!',
      confirmation: 'DELETE'
    });
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('prevents mismatched passwords', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;

    component.passwordForm.setValue({
      currentPassword: 'PreviousPassword123!',
      newPassword: 'NewPassword123!',
      confirmPassword: 'DifferentPassword123!'
    });

    component.changePassword();

    expect(mockAccount.changePassword).not.toHaveBeenCalled();
    expect(component.passwordError()).toContain('do not match');
  });

  it('prevents submitting a weak password', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;

    component.passwordForm.setValue({
      currentPassword: 'PreviousPassword123!',
      newPassword: '123',
      confirmPassword: '123'
    });

    component.changePassword();

    expect(component.passwordForm.invalid).toBe(true);
    expect(mockAccount.changePassword).not.toHaveBeenCalled();
  });

  it('keeps accent independent from appearance', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const component = fixture.componentInstance;

    component.changeTheme('dark');
    component.changeAccent('purple');

    expect(component.theme.preference()).toBe('dark');
    expect(component.theme.accent()).toBe('purple');
  });
});
