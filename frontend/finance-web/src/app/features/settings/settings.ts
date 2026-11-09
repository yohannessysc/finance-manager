
import {
  Component,
  computed,
  inject,
  OnInit,
  signal
} from '@angular/core';

import { DatePipe } from '@angular/common';

import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';

import { HttpErrorResponse } from '@angular/common/http';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';

import { AuthService } from '../../core/auth/auth.service';

import {
  AccountProfile,
  AccountSettingsService
} from '../../core/services/account-settings.service';

import {
  AccentColor,
  ThemePreference,
  ThemeService
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

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, DatePipe],
  templateUrl: './settings.html',
  styleUrl: './settings.scss'
})
export class Settings implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly accountSettings = inject(AccountSettingsService);
  private readonly accountPrivacyService = inject(AccountPrivacyService);
  private readonly userPreferencesService =
    inject(UserPreferencesService);
  private readonly sessionsService = inject(SessionsService);

  readonly sessions = signal<UserSession[]>([]);
  readonly loadingSessions = signal(true);
  readonly sessionsBusy = signal(false);
  readonly revokingSession = signal<string | null>(null);
  readonly sessionsMessage = signal('');
  readonly sessionsError = signal('');

  readonly hasOtherSessions = computed(() =>
    this.sessions().some(item => !item.isCurrent)
  );

  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);

  readonly profile = signal<AccountProfile | null>(null);
  readonly loadingProfile = signal(true);
  readonly savingProfile = signal(false);
  readonly changingPassword = signal(false);

  readonly profileMessage = signal('');
  readonly profileError = signal('');
  readonly passwordError = signal('');
  readonly emailChangeBusy = signal(false);
  readonly emailChangeMessage = signal('');
  readonly emailChangeError = signal('');

  readonly exportingData = signal(false);
  readonly deletingAccount = signal(false);
  readonly privacyMessage = signal('');
  readonly privacyError = signal('');

  readonly preferences = signal<UserPreferences | null>(null);

  readonly loadingPreferences = signal(true);
  readonly savingPreferences = signal(false);

  readonly preferencesMessage = signal('');
  readonly preferencesError = signal('');

  readonly supportedTimeZones = [
    { value: 'America/Toronto', label: 'Eastern — Toronto/Ottawa' },
    { value: 'America/Halifax', label: 'Atlantic — Halifax' },
    { value: 'America/St_Johns', label: 'Newfoundland' },
    { value: 'America/Winnipeg', label: 'Central — Winnipeg' },
    { value: 'America/Edmonton', label: 'Mountain — Edmonton' },
    { value: 'America/Vancouver', label: 'Pacific — Vancouver' },
    { value: 'America/New_York', label: 'US Eastern' },
    { value: 'America/Chicago', label: 'US Central' },
    { value: 'America/Denver', label: 'US Mountain' },
    { value: 'America/Los_Angeles', label: 'US Pacific' },
    { value: 'UTC', label: 'UTC' }
  ];

  readonly themes: {
    value: ThemePreference;
    label: string;
    description: string;
  }[] = [
    {
      value: 'light',
      label: 'Light',
      description: 'Bright backgrounds'
    },
    {
      value: 'dark',
      label: 'Dark',
      description: 'Dark backgrounds'
    },
    {
      value: 'system',
      label: 'System',
      description: 'Follow your device'
    }
  ];

  readonly profileForm = this.fb.nonNullable.group({
    firstName: [
      '',
      [Validators.required, Validators.maxLength(100)]
    ],
    lastName: [
      '',
      [Validators.required, Validators.maxLength(100)]
    ]
  });

  readonly passwordForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: [
      '',
      [
        Validators.required,
        Validators.minLength(12),
        Validators.pattern(
          /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).+$/
        )
      ]
    ],
    confirmPassword: ['', Validators.required]
  });

  readonly emailChangeForm = this.fb.nonNullable.group({
    newEmail: ['', [Validators.required, Validators.email, Validators.maxLength(254)]],
    currentPassword: ['', Validators.required]
  });

  readonly deleteAccountForm = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    confirmation: ['', Validators.required]
  });

  readonly preferencesForm = this.fb.nonNullable.group({
    defaultCurrency: ['CAD', Validators.required],
    timeZone: ['America/Toronto', Validators.required],
    dateFormat: ['yyyy-MM-dd', Validators.required],
    budgetStartDay: [
      1,
      [
        Validators.required,
        Validators.min(1),
        Validators.max(28)
      ]
    ]
  });

  ngOnInit(): void {
    this.loadProfile();
    this.loadPreferences();
    this.loadSessions();
  }

  loadSessions(): void {
    this.loadingSessions.set(true);
    this.sessionsError.set('');

    this.sessionsService.list()
      .pipe(finalize(() => this.loadingSessions.set(false)))
      .subscribe({
        next: items => this.sessions.set(items),
        error: (error: HttpErrorResponse) => {
          this.sessionsError.set(
            this.getError(
              error,
              'Unable to load your active sessions.'
            )
          );
        }
      });
  }

  revokeSession(item: UserSession): void {
    if (this.revokingSession() || item.isCurrent) return;

    this.revokingSession.set(item.id);
    this.sessionsMessage.set('');
    this.sessionsError.set('');

    this.sessionsService.revoke(item.id)
      .pipe(finalize(() => this.revokingSession.set(null)))
      .subscribe({
        next: () => {
          this.sessions.update(list =>
            list.filter(x => x.id !== item.id)
          );
          this.sessionsMessage.set('The session has been signed out.');
        },
        error: (error: HttpErrorResponse) => {
          this.sessionsError.set(
            this.getError(error, 'Unable to revoke that session.')
          );
        }
      });
  }

  signOutOtherSessions(): void {
    if (this.sessionsBusy()) return;

    this.sessionsBusy.set(true);
    this.sessionsMessage.set('');
    this.sessionsError.set('');

    this.sessionsService.revokeOthers()
      .pipe(finalize(() => this.sessionsBusy.set(false)))
      .subscribe({
        next: () => {
          this.sessions.update(list =>
            list.filter(x => x.isCurrent)
          );
          this.sessionsMessage.set(
            'All other devices have been signed out.'
          );
        },
        error: (error: HttpErrorResponse) => {
          this.sessionsError.set(
            this.getError(error, 'Unable to sign out other devices.')
          );
        }
      });
  }

  signOutEverywhere(): void {
    if (this.sessionsBusy()) return;

    this.sessionsBusy.set(true);
    this.sessionsMessage.set('');
    this.sessionsError.set('');

    this.sessionsService.revokeAll()
      .pipe(finalize(() => this.sessionsBusy.set(false)))
      .subscribe({
        next: () => {
          // The API has also signed out this browser.
          this.auth.currentUser.set(null);
          this.router.navigate(['/login']);
        },
        error: (error: HttpErrorResponse) => {
          this.sessionsError.set(
            this.getError(error, 'Unable to sign out everywhere.')
          );
        }
      });
  }

  loadPreferences(): void {
    this.loadingPreferences.set(true);
    this.preferencesError.set('');

    this.userPreferencesService.get()
      .pipe(finalize(() => this.loadingPreferences.set(false)))
      .subscribe({
        next: response => {
          this.preferences.set(response);

          this.preferencesForm.setValue({
            defaultCurrency: response.defaultCurrency,
            timeZone: response.timeZone,
            dateFormat: response.dateFormat,
            budgetStartDay: response.budgetStartDay
          });
        },
        error: (error: HttpErrorResponse) => {
          this.preferencesError.set(
            this.getError(
              error,
              'Unable to load your financial preferences.'
            )
          );
        }
      });
  }

  savePreferences(): void {
    if (this.savingPreferences()) return;

    if (this.preferencesForm.invalid) {
      this.preferencesForm.markAllAsTouched();
      return;
    }

    this.savingPreferences.set(true);
    this.preferencesMessage.set('');
    this.preferencesError.set('');

    this.userPreferencesService
      .update(this.preferencesForm.getRawValue())
      .pipe(finalize(() => this.savingPreferences.set(false)))
      .subscribe({
        next: response => {
          this.preferences.set(response);

          this.preferencesMessage.set(
            'Your financial preferences have been saved.'
          );
        },
        error: (error: HttpErrorResponse) => {
          this.preferencesError.set(
            this.getError(
              error,
              'Unable to save your financial preferences.'
            )
          );
        }
      });
  }

  loadProfile(): void {
    this.loadingProfile.set(true);
    this.profileError.set('');

    this.accountSettings.getProfile()
      .pipe(finalize(() => this.loadingProfile.set(false)))
      .subscribe({
        next: result => {
          this.profile.set(result);

          this.profileForm.setValue({
            firstName: result.firstName,
            lastName: result.lastName
          });
        },
        error: (error: HttpErrorResponse) => {
          this.profileError.set(
            this.getError(
              error,
              'Unable to load your account profile.'
            )
          );
        }
      });
  }

  saveProfile(): void {
    if (this.savingProfile()) return;

    if (this.profileForm.invalid) {
      this.profileForm.markAllAsTouched();
      return;
    }

    const firstName =
      this.profileForm.controls.firstName.value.trim();

    const lastName =
      this.profileForm.controls.lastName.value.trim();

    if (!firstName || !lastName) {
      this.profileForm.markAllAsTouched();
      this.profileError.set('Both names are required.');
      return;
    }

    this.savingProfile.set(true);
    this.profileMessage.set('');
    this.profileError.set('');

    this.accountSettings.updateProfile({
      firstName,
      lastName
    })
      .pipe(finalize(() => this.savingProfile.set(false)))
      .subscribe({
        next: updated => {
          this.profile.set(updated);

          // Refresh the existing user information used
          // elsewhere in Finance Manager.
          this.auth.currentUser.update(current =>
            current
              ? {
                  ...current,
                  firstName: updated.firstName,
                  lastName: updated.lastName
                }
              : current
          );

          this.profileForm.setValue({
            firstName: updated.firstName,
            lastName: updated.lastName
          });

          this.profileMessage.set(
            'Your profile has been updated.'
          );
        },
        error: (error: HttpErrorResponse) => {
          this.profileError.set(
            this.getError(
              error,
              'Unable to save your profile.'
            )
          );
        }
      });
  }

  changePassword(): void {
    if (this.changingPassword()) return;

    this.passwordError.set('');

    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const values = this.passwordForm.getRawValue();

    if (values.newPassword !== values.confirmPassword) {
      this.passwordError.set(
        'The new passwords do not match.'
      );
      return;
    }

    if (values.currentPassword === values.newPassword) {
      this.passwordError.set(
        'Choose a password different from your current password.'
      );
      return;
    }

    this.changingPassword.set(true);

    this.accountSettings.changePassword({
      currentPassword: values.currentPassword,
      newPassword: values.newPassword
    })
      .pipe(finalize(() => this.changingPassword.set(false)))
      .subscribe({
        next: () => {
          // The API has signed out this browser.
          // Clear the cached frontend identity.
          this.auth.currentUser.set(null);
          this.passwordForm.reset();

          this.router.navigate(['/login']);
        },
        error: (error: HttpErrorResponse) => {
          this.passwordError.set(
            this.getError(
              error,
              'Unable to change your password.'
            )
          );
        }
      });
  }

  requestEmailChange(): void {
    if (this.emailChangeBusy()) return;

    this.emailChangeError.set('');
    this.emailChangeMessage.set('');

    if (this.emailChangeForm.invalid) {
      this.emailChangeForm.markAllAsTouched();
      return;
    }

    this.emailChangeBusy.set(true);

    this.accountSettings
      .requestEmailChange(this.emailChangeForm.getRawValue())
      .pipe(finalize(() => this.emailChangeBusy.set(false)))
      .subscribe({
        next: result => {
          this.emailChangeForm.reset();
          this.emailChangeMessage.set(result.message);
        },
        error: (error: HttpErrorResponse) => {
          this.emailChangeError.set(
            this.getError(error, 'Unable to request an email change.')
          );
        }
      });
  }

  exportFinancialData(): void {
    if (this.exportingData()) return;

    this.exportingData.set(true);
    this.privacyError.set('');
    this.privacyMessage.set('');

    this.accountPrivacyService
      .exportFinancialData()
      .pipe(finalize(() => this.exportingData.set(false)))
      .subscribe({
        next: blob => {
          const url = URL.createObjectURL(blob);
          const link = document.createElement('a');
          link.href = url;
          link.download = `finance-manager-export-${new Date()
            .toISOString()
            .slice(0, 10)}.json`;
          link.click();
          URL.revokeObjectURL(url);
          this.privacyMessage.set('Your financial data export has downloaded.');
        },
        error: (error: HttpErrorResponse) => {
          this.privacyError.set(
            this.getError(error, 'Unable to export your financial data.')
          );
        }
      });
  }

  deleteAccount(): void {
    if (this.deletingAccount()) return;

    this.privacyError.set('');
    this.privacyMessage.set('');

    if (this.deleteAccountForm.invalid) {
      this.deleteAccountForm.markAllAsTouched();
      return;
    }

    const request = this.deleteAccountForm.getRawValue();

    if (request.confirmation !== 'DELETE') {
      this.privacyError.set('Type DELETE exactly to confirm account deletion.');
      return;
    }

    this.deletingAccount.set(true);

    this.accountPrivacyService
      .deleteAccount(request)
      .pipe(finalize(() => this.deletingAccount.set(false)))
      .subscribe({
        next: () => {
          this.auth.currentUser.set(null);
          this.router.navigate(['/login']);
        },
        error: (error: HttpErrorResponse) => {
          this.privacyError.set(
            this.getError(error, 'Unable to delete your account.')
          );
        }
      });
  }

  changeTheme(value: ThemePreference): void {
    this.theme.setTheme(value);
  }

  changeAccent(value: string): void {
    const selected = this.theme.accents.find(
      item => item.id === value
    );

    if (selected) {
      this.theme.setAccent(selected.id as AccentColor);
    }
  }

  private getError(
    error: HttpErrorResponse,
    fallback: string
  ): string {
    if (error.status === 401) {
      return 'Your session has expired. Please sign in again.';
    }

    if (typeof error.error?.message === 'string') {
      return error.error.message;
    }

    return fallback;
  }
}
