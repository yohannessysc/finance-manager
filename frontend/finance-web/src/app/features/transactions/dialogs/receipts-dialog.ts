import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, OnInit, output, signal } from '@angular/core';

import { RecentTransaction } from '../../dashboard/dashboard.model';
import { Attachment, TransactionsService } from '../transactions.service';

const MAX_BYTES = 5 * 1024 * 1024;
// Photos above this are shrunk in the browser before upload.
const SHRINK_ABOVE_BYTES = 1.5 * 1024 * 1024;
const MAX_DIMENSION = 2000;

// Receipt photos and PDFs attached to one transaction.
@Component({
  selector: 'app-receipts-dialog',
  standalone: true,
  imports: [DatePipe],
  styleUrl: './dialog.scss',
  template: `
    <div class="modal-backdrop">
      <section class="modal receipts-modal" role="dialog" aria-modal="true" aria-labelledby="receipts-title">
        <div class="modal-header">
          <div>
            <h2 id="receipts-title">Receipts</h2>
            <p>{{ transaction().description }} · {{ transaction().transactionDateUtc | date: 'MMM d, y' }}</p>
          </div>
          <button type="button" class="close-button" aria-label="Close" [disabled]="busy()" (click)="close()">×</button>
        </div>

        @if (loading()) {
          <p class="muted">Loading…</p>
        } @else if (attachments().length === 0) {
          <p class="muted">No receipts yet. Add a photo or PDF to keep it with this transaction.</p>
        } @else {
          <ul class="receipts">
            @for (attachment of attachments(); track attachment.id) {
              <li>
                <a [href]="urlFor(attachment)" target="_blank" rel="noopener" class="preview"
                   [attr.aria-label]="'Open ' + attachment.fileName">
                  @if (isPreviewable(attachment)) {
                    <img [src]="urlFor(attachment)" alt="" loading="lazy" />
                  } @else {
                    <span class="file-icon">{{ attachment.contentType === 'application/pdf' ? 'PDF' : 'IMG' }}</span>
                  }
                </a>
                <div class="details">
                  <a [href]="urlFor(attachment)" target="_blank" rel="noopener">{{ attachment.fileName }}</a>
                  <span class="muted">{{ size(attachment.sizeBytes) }} · added {{ attachment.createdAtUtc | date: 'MMM d, y' }}</span>
                </div>
                <button type="button" class="text-button danger" [disabled]="busy()"
                        [attr.aria-label]="'Delete ' + attachment.fileName" (click)="remove(attachment)">Delete</button>
              </li>
            }
          </ul>
        }

        @if (error()) {
          <p class="form-error" role="alert">{{ error() }}</p>
        }

        <div class="modal-actions">
          <label class="btn primary upload" [class.disabled]="busy() || attachments().length >= 10">
            {{ uploading() ? 'Uploading…' : '+ Add receipt' }}
            <input type="file" accept="image/*,application/pdf" [disabled]="busy() || attachments().length >= 10"
                   (change)="upload($event)" />
          </label>
          <button type="button" class="btn secondary" [disabled]="busy()" (click)="close()">Done</button>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .receipts-modal { max-width: 560px; }
    .receipts { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 12px; }
    .receipts li { display: flex; align-items: center; gap: 12px; }
    .preview {
      flex: none; width: 56px; height: 56px; border-radius: 10px; overflow: hidden;
      border: 1px solid var(--fm-border); display: grid; place-items: center; background: var(--fm-hover);
    }
    .preview img { width: 100%; height: 100%; object-fit: cover; }
    .file-icon { font-size: 11px; font-weight: 800; color: var(--fm-muted); }
    .details { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 3px; }
    .details a { color: var(--fm-text); font-weight: 700; font-size: 13px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .upload { position: relative; overflow: hidden; display: inline-flex; align-items: center; }
    .upload input { position: absolute; inset: 0; opacity: 0; cursor: pointer; }
    .upload.disabled { opacity: .55; }
  `]
})
export class ReceiptsDialog implements OnInit {
  private readonly service = inject(TransactionsService);

  readonly transaction = input.required<RecentTransaction>();

  // Emits the number of receipts when the dialog closes.
  readonly closed = output<number>();

  readonly attachments = signal<Attachment[]>([]);
  readonly loading = signal(true);
  readonly uploading = signal(false);
  readonly deleting = signal(false);
  readonly error = signal('');

  busy(): boolean {
    return this.uploading() || this.deleting();
  }

  ngOnInit(): void {
    this.service.listAttachments(this.transaction().id).subscribe({
      next: attachments => {
        this.attachments.set(attachments);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Could not load receipts.');
        this.loading.set(false);
      }
    });
  }

  urlFor(attachment: Attachment): string {
    return `${this.service.attachmentsUrl(this.transaction().id)}/${attachment.id}`;
  }

  // Browsers other than Safari can't display HEIC.
  isPreviewable(attachment: Attachment): boolean {
    return ['image/jpeg', 'image/png', 'image/webp'].includes(attachment.contentType);
  }

  size(bytes: number): string {
    return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  async upload(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file || this.busy()) return;

    this.error.set('');
    this.uploading.set(true);

    try {
      const prepared = await this.shrink(file);
      if (prepared.blob.size > MAX_BYTES) {
        throw new Error('Receipts cannot exceed 5 MB.');
      }

      this.service.uploadAttachment(this.transaction().id, prepared.blob, prepared.name).subscribe({
        next: attachment => {
          this.attachments.update(items => [...items, attachment]);
          this.uploading.set(false);
        },
        error: (err: HttpErrorResponse) => {
          this.error.set(err.error?.message ?? 'Upload failed. Please try again.');
          this.uploading.set(false);
        }
      });
    } catch (err) {
      this.error.set(err instanceof Error ? err.message : 'Upload failed.');
      this.uploading.set(false);
    }
  }

  remove(attachment: Attachment): void {
    if (this.busy()) return;
    this.deleting.set(true);
    this.error.set('');

    this.service.deleteAttachment(this.transaction().id, attachment.id).subscribe({
      next: () => {
        this.attachments.update(items => items.filter(item => item.id !== attachment.id));
        this.deleting.set(false);
      },
      error: () => {
        this.error.set('Could not delete the receipt.');
        this.deleting.set(false);
      }
    });
  }

  close(): void {
    if (this.busy()) return;
    this.closed.emit(this.attachments().length);
  }

  // Large phone photos are scaled down to JPEG so they upload quickly
  // and stay small. PDFs, HEIC and small images are sent unchanged.
  private async shrink(file: File): Promise<{ blob: Blob; name: string }> {
    const resizable = ['image/jpeg', 'image/png', 'image/webp'].includes(file.type);
    if (!resizable || file.size <= SHRINK_ABOVE_BYTES || typeof createImageBitmap !== 'function') {
      return { blob: file, name: file.name };
    }

    const bitmap = await createImageBitmap(file);
    const scale = Math.min(1, MAX_DIMENSION / Math.max(bitmap.width, bitmap.height));
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    bitmap.close();

    const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', 0.85));
    if (!blob || blob.size >= file.size) {
      return { blob: file, name: file.name };
    }

    return { blob, name: file.name.replace(/\.[^.]+$/, '') + '.jpg' };
  }
}
