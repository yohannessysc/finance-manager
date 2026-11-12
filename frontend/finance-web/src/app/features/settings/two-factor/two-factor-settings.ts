import { NgTemplateOutlet } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { finalize, Observable } from 'rxjs';
import { toDataURL } from 'qrcode';

import {
  TwoFactorService,
  TwoFactorSetup,
  TwoFactorStatus
} from './two-factor.service';

type Mode = 'idle' | 'setup' | 'disable' | 'regenerate';

@Component({
  selector: 'app-two-factor-settings',
  standalone: true,
  imports: [ReactiveFormsModule, NgTemplateOutlet],
  templateUrl: './two-factor-settings.html',
  styleUrl: './two-factor-settings.scss'
})
export class TwoFactorSettings implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly twoFactor = inject(TwoFactorService);

  readonly status = signal<TwoFactorStatus | null>(null);
  readonly mode = signal<Mode>('idle');
  readonly setup = signal<TwoFactorSetup | null>(null);
  readonly qrCode = signal('');
  // Shown once, right after they are generated.
  readonly recoveryCodes = signal<string[]>([]);
  readonly busy = signal(false);
  readonly error = signal('');

  readonly codeForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^\s*\d{3}\s?\d{3}\s*$/)]]
  });

  readonly passwordForm = this.fb.nonNullable.group({
    password: ['', Validators.required]
  });

  ngOnInit(): void {
    this.loadStatus();
  }

  startSetup(): void {
    this.run(this.twoFactor.setup(), async setup => {
      this.setup.set(setup);
      this.qrCode.set(await toDataURL(setup.authenticatorUri, { margin: 1, width: 180 }));
      this.codeForm.reset();
      this.mode.set('setup');
    });
  }

  confirmSetup(): void {
    if (this.codeForm.invalid) {
      this.codeForm.markAllAsTouched();
      return;
    }

    this.run(this.twoFactor.enable(this.codeForm.getRawValue().code.replace(/\s/g, '')), result => {
      this.recoveryCodes.set(result.recoveryCodes);
      this.setup.set(null);
      this.mode.set('idle');
      this.loadStatus();
    });
  }

  ask(mode: 'disable' | 'regenerate'): void {
    this.passwordForm.reset();
    this.error.set('');
    this.mode.set(mode);
  }

  confirmPassword(): void {
    if (this.passwordForm.invalid) {
      this.passwordForm.markAllAsTouched();
      return;
    }

    const password = this.passwordForm.getRawValue().password;

    if (this.mode() === 'disable') {
      this.run(this.twoFactor.disable(password), () => {
        this.recoveryCodes.set([]);
        this.mode.set('idle');
        this.loadStatus();
      });
    } else {
      this.run(this.twoFactor.regenerateRecoveryCodes(password), result => {
        this.recoveryCodes.set(result.recoveryCodes);
        this.mode.set('idle');
        this.loadStatus();
      });
    }
  }

  cancel(): void {
    this.mode.set('idle');
    this.setup.set(null);
    this.error.set('');
  }

  downloadCodes(): void {
    const text = [
      'Finance Manager recovery codes',
      'Each code can be used once to sign in if you lose your authenticator.',
      '',
      ...this.recoveryCodes()
    ].join('\n');
    const link = document.createElement('a');
    link.href = URL.createObjectURL(new Blob([text], { type: 'text/plain' }));
    link.download = 'finance-manager-recovery-codes.txt';
    link.click();
    URL.revokeObjectURL(link.href);
  }

  dismissCodes(): void {
    this.recoveryCodes.set([]);
  }

  private loadStatus(): void {
    this.twoFactor.getStatus().subscribe({
      next: status => this.status.set(status),
      error: (error: HttpErrorResponse) => this.error.set(this.message(error))
    });
  }

  private run<T>(request: Observable<T>, onSuccess: (result: T) => void | Promise<void>): void {
    this.busy.set(true);
    this.error.set('');

    request.pipe(finalize(() => this.busy.set(false))).subscribe({
      next: result => void onSuccess(result),
      error: (error: HttpErrorResponse) => this.error.set(this.message(error))
    });
  }

  private message(error: HttpErrorResponse): string {
    return typeof error.error?.message === 'string'
      ? error.error.message
      : 'Something went wrong. Please try again.';
  }
}
