
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../core/auth/auth.service';
import {
  AccentColor,
  ThemePreference,
  ThemeService
} from '../../core/services/theme.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './settings.html',
  styleUrl: './settings.scss'
})
export class Settings {
  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);

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

  fullName(): string {
    const user = this.auth.currentUser();

    if (!user) return 'Not available';

    return [user.firstName, user.lastName]
      .filter(Boolean)
      .join(' ')
      .trim() || 'Not provided';
  }

  changeTheme(value: ThemePreference): void {
    this.theme.setTheme(value);
  }

  changeAccent(value: string): void {
    const option = this.theme.accents.find(
      item => item.id === value
    );

    if (option) {
      this.theme.setAccent(option.id as AccentColor);
    }
  }
}
