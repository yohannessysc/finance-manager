
import { Injectable, effect, signal } from '@angular/core';

export type ThemePreference = 'light' | 'dark' | 'system';

export type AccentColor =
  | 'emerald'
  | 'ocean'
  | 'purple'
  | 'rose'
  | 'coral'
  | 'amber'
  | 'slate';

export interface AccentOption {
  id: AccentColor;
  name: string;
  hex: string;
  soft: string;
}

export const ACCENT_OPTIONS: readonly AccentOption[] = [
  {
    id: 'emerald',
    name: 'Emerald',
    hex: '#13866B',
    soft: 'rgba(19,134,107,0.13)'
  },
  {
    id: 'ocean',
    name: 'Ocean',
    hex: '#2563EB',
    soft: 'rgba(37,99,235,0.13)'
  },
  {
    id: 'purple',
    name: 'Purple',
    hex: '#7856D8',
    soft: 'rgba(120,86,216,0.13)'
  },
  {
    id: 'rose',
    name: 'Rose',
    hex: '#BE3970',
    soft: 'rgba(190,57,112,0.13)'
  },
  {
    id: 'coral',
    name: 'Coral',
    hex: '#C45137',
    soft: 'rgba(196,81,55,0.13)'
  },
  {
    id: 'amber',
    name: 'Amber',
    hex: '#976000',
    soft: 'rgba(151,96,0,0.13)'
  },
  {
    id: 'slate',
    name: 'Slate',
    hex: '#475569',
    soft: 'rgba(71,85,105,0.13)'
  }
];

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly themeKey = 'finance-manager-theme';
  private readonly accentKey = 'finance-manager-accent';

  readonly preference = signal<ThemePreference>(
    this.getSavedTheme()
  );

  readonly accent = signal<AccentColor>(
    this.getSavedAccent()
  );

  readonly resolvedTheme = signal<'light' | 'dark'>('light');

  readonly accents = ACCENT_OPTIONS;

  constructor() {
    if (typeof window !== 'undefined') {
      window.matchMedia('(prefers-color-scheme: dark)')
        .addEventListener('change', () => this.applyTheme());
    }

    effect(() => {
      this.preference();
      this.accent();
      this.applyTheme();
    });
  }

  setTheme(value: ThemePreference): void {
    this.preference.set(value);

    if (typeof window !== 'undefined') {
      localStorage.setItem(this.themeKey, value);
    }
  }

  setAccent(value: AccentColor): void {
    if (!ACCENT_OPTIONS.some(option => option.id === value)) {
      return;
    }

    // Changing accent NEVER changes Light/Dark/System.
    this.accent.set(value);

    if (typeof window !== 'undefined') {
      localStorage.setItem(this.accentKey, value);
    }
  }

  private getSavedTheme(): ThemePreference {
    if (typeof window === 'undefined') return 'system';

    const saved = localStorage.getItem(this.themeKey);

    if (
      saved === 'light' ||
      saved === 'dark' ||
      saved === 'system'
    ) {
      return saved;
    }

    // Compatibility with the previous Custom mode.
    if (saved === 'custom') return 'system';

    return 'system';
  }

  private getSavedAccent(): AccentColor {
    if (typeof window === 'undefined') return 'emerald';

    const saved = localStorage.getItem(this.accentKey);
    const option = ACCENT_OPTIONS.find(x => x.id === saved);

    return option?.id ?? 'emerald';
  }

  private applyTheme(): void {
    if (typeof window === 'undefined') return;

    const preference = this.preference();

    const systemDark = window.matchMedia(
      '(prefers-color-scheme: dark)'
    ).matches;

    const dark =
      preference === 'dark' ||
      (preference === 'system' && systemDark);

    const resolved = dark ? 'dark' : 'light';

    this.resolvedTheme.set(resolved);

    const root = document.documentElement;

    root.setAttribute('data-theme', resolved);
    root.style.colorScheme = resolved;

    const selected = ACCENT_OPTIONS.find(
      option => option.id === this.accent()
    ) ?? ACCENT_OPTIONS[0];

    root.style.setProperty('--fm-primary', selected.hex);
    root.style.setProperty('--fm-primary-soft', selected.soft);
    root.style.setProperty('--fm-accent', selected.hex);
  }
}
