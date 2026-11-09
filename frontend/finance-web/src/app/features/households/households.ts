import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  FormBuilder,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { finalize } from 'rxjs';

import {
  Household,
  HouseholdMember,
  HouseholdsService
} from './households.service';

@Component({
  selector: 'app-households',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './households.html',
  styleUrl: './households.scss'
})
export class Households implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly service = inject(HouseholdsService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly message = signal('');
  readonly households = signal<Household[]>([]);

  readonly createForm = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]]
  });

  readonly inviteForm = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    role: ['Editor' as 'Admin' | 'Editor' | 'Viewer', Validators.required]
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    this.service.getAll()
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: items => this.households.set(items),
        error: error => this.error.set(
          this.getError(error, 'Unable to load household settings.')
        )
      });
  }

  create(): void {
    if (this.saving()) return;
    if (this.createForm.invalid) {
      this.createForm.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.service.create(this.createForm.controls.name.value.trim())
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: household => {
          this.households.update(items => [...items, household]);
          this.createForm.reset({ name: '' });
          this.message.set('Household created. Accounts remain private until shared.');
        },
        error: error => this.error.set(
          this.getError(error, 'Unable to create the household.')
        )
      });
  }

  invite(household: Household): void {
    if (this.saving()) return;
    if (this.inviteForm.invalid) {
      this.inviteForm.markAllAsTouched();
      return;
    }

    const value = this.inviteForm.getRawValue();
    if (value.role === 'Admin' && !household.canAssignAdmin) {
      this.error.set('Only the household owner can invite an admin.');
      return;
    }

    this.saving.set(true);
    this.error.set('');
    this.service.invite(household.id, value.email.trim(), value.role)
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: invitation => {
          this.inviteForm.reset({ email: '', role: 'Editor' });
          this.message.set(
            `Invitation sent to ${invitation.email}. It expires ` +
            `${new Date(invitation.expiresAtUtc).toLocaleDateString()}.`
          );
        },
        error: error => this.error.set(
          this.getError(error, 'Unable to send the household invitation.')
        )
      });
  }

  changeRole(
    household: Household,
    member: HouseholdMember,
    value: string
  ): void {
    if (value !== 'Admin' && value !== 'Editor' && value !== 'Viewer')
      return;

    this.service.updateRole(household.id, member.userId, value)
      .subscribe({
        next: () => {
          this.message.set(`${member.email}'s role was updated.`);
          this.load();
        },
        error: error => this.error.set(
          this.getError(error, 'Unable to update this member role.')
        )
      });
  }

  removeMember(household: Household, member: HouseholdMember): void {
    this.service.removeMember(household.id, member.userId)
      .subscribe({
        next: () => {
          this.message.set(`${member.email} was removed from the household.`);
          this.load();
        },
        error: error => this.error.set(
          this.getError(error, 'Unable to remove this household member.')
        )
      });
  }

  private getError(error: HttpErrorResponse, fallback: string): string {
    if (error.status === 401)
      return 'Your session has expired. Please sign in again.';
    if (typeof error.error?.message === 'string')
      return error.error.message;
    return fallback;
  }
}
