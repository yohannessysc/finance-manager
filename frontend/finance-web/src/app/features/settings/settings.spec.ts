
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { signal } from '@angular/core';
import { vi } from 'vitest';

import { Settings } from './settings';
import { AuthService } from '../../core/auth/auth.service';
import {
  ACCENT_OPTIONS,
  ThemeService,
  ThemePreference,
  AccentColor
} from '../../core/services/theme.service';

describe('Settings', () => {
  let currentUser: ReturnType<typeof signal<{
    id: string;
    firstName: string;
    lastName: string;
    email: string;
  }>>;

  let preference: ReturnType<typeof signal<ThemePreference>>;
  let accent: ReturnType<typeof signal<AccentColor>>;

  let setTheme: ReturnType<typeof vi.fn>;
  let setAccent: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    currentUser = signal({
      id: '11111111-1111-4111-8111-111111111111',
      firstName: 'Test',
      lastName: 'User',
      email: 'test@example.com'
    });

    preference = signal<ThemePreference>('system');
    accent = signal<AccentColor>('emerald');

    setTheme = vi.fn((value: ThemePreference) => {
      preference.set(value);
    });

    setAccent = vi.fn((value: AccentColor) => {
      accent.set(value);
    });

    await TestBed.configureTestingModule({
      imports: [Settings],
      providers: [
        provideRouter([]),
        {
          provide: AuthService,
          useValue: { currentUser }
        },
        {
          provide: ThemeService,
          useValue: {
            preference,
            accent,
            accents: ACCENT_OPTIONS,
            resolvedTheme: signal<'light' | 'dark'>('light'),
            setTheme,
            setAccent
          }
        }
      ]
    }).compileComponents();
  });

  it('shows the signed-in user profile', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const text = (fixture.nativeElement as HTMLElement)
      .textContent ?? '';

    expect(text).toContain('Test User');
    expect(text).toContain('test@example.com');
  });

  it('switches appearance without changing accent', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    fixture.componentInstance.changeTheme('dark');

    expect(setTheme).toHaveBeenCalledWith('dark');
    expect(preference()).toBe('dark');
    expect(accent()).toBe('emerald');
  });

  it('changes accent without changing appearance', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    fixture.componentInstance.changeTheme('dark');
    fixture.componentInstance.changeAccent('purple');

    expect(setAccent).toHaveBeenCalledWith('purple');
    expect(accent()).toBe('purple');
    expect(preference()).toBe('dark');
  });

  it('rejects invalid colour selections', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    fixture.componentInstance.changeAccent('invalid');

    expect(setAccent).not.toHaveBeenCalled();
    expect(accent()).toBe('emerald');
  });

  it('provides password recovery', () => {
    const fixture = TestBed.createComponent(Settings);
    fixture.detectChanges();

    const link = (fixture.nativeElement as HTMLElement)
      .querySelector('a[href="/forgot-password"]');

    expect(link).not.toBeNull();
  });
});
