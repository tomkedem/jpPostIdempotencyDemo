import { Component, Inject, ViewEncapsulation } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { CleanupPreview } from '../../services/data-cleanup.service';

export interface CleanupConfirmationData {
  preview?: CleanupPreview;
  step: 'first' | 'second';
}

@Component({
  selector: 'app-cleanup-confirmation-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule
  ],
  encapsulation: ViewEncapsulation.None,
  template: `
    <div class="cleanup-dialog" [ngClass]="data.step === 'first' ? 'step-first' : 'step-second'">
      <!-- Header -->
      <div class="dialog-header">
        <div class="warning-icon-container">
          <mat-icon class="warning-icon">{{ data.step === 'first' ? 'warning' : 'delete_forever' }}</mat-icon>
        </div>
        <h2 class="dialog-title">
          {{ data.step === 'first' ? 'אזהרה קריטית!' : 'אישור סופי למחיקה' }}
        </h2>
        <p class="dialog-subtitle">
          {{ data.step === 'first' ? 'פעולה זו תמחק לצמיתות את כל הנתונים ממסד הנתונים' : 'זוהי ההזדמנות האחרונה לבטל' }}
        </p>
      </div>

      <!-- Content -->
      <div class="dialog-content">
        <div class="warning-banner">
          <mat-icon>error_outline</mat-icon>
          <span>לא ניתן לשחזר את הנתונים לאחר המחיקה!</span>
        </div>

        <!-- First Step Content -->
        <div *ngIf="data.step === 'first'" class="data-types">
          <div class="data-type-item">
            <mat-icon>vpn_key</mat-icon>
            <div class="item-content">
              <span class="item-title">מפתחות אידמפוטנטיות</span>
              <span class="item-description">כל מפתחות ההגנה מכפילויות</span>
            </div>
          </div>
          <div class="data-type-item">
            <mat-icon>insights</mat-icon>
            <div class="item-content">
              <span class="item-title">מדדים היסטוריים</span>
              <span class="item-description">כל נתוני הביצועים והסטטיסטיקות</span>
            </div>
          </div>
          <div class="data-type-item">
            <mat-icon>history</mat-icon>
            <div class="item-content">
              <span class="item-title">רישומי פעולות</span>
              <span class="item-description">כל היסטוריית הפעילות במערכת</span>
            </div>
          </div>
        </div>

        <!-- Second Step Content -->
        <div *ngIf="data.step === 'second' && data.preview">
          <h3>פרטי המחיקה:</h3>
          <ul>
            <li>{{ data.preview.idempotencyEntriesCount | number }} רשומות אידמפוטנטיות</li>
            <li>{{ data.preview.operationMetricsCount | number }} רשומות מדדים</li>
            <li>גודל משוער: {{ data.preview.estimatedDataSizeKB | number }} KB</li>
            <li>נתונים מתאריך: {{ formatDate(data.preview.oldestIdempotencyEntry) }}</li>
          </ul>
        </div>
      </div>

      <!-- Actions -->
      <div class="dialog-actions">
        <button 
          mat-stroked-button 
          class="cancel-btn"
          (click)="onCancel()">
          ביטול
        </button>
        
        <button 
          mat-flat-button 
          class="confirm-btn"
          [ngClass]="data.step === 'first' ? 'step-first' : 'step-second'"
          (click)="onConfirm()">
          {{ data.step === 'first' ? 'המשך' : 'מחק הכל' }}
        </button>
      </div>
    </div>
  `,
  styleUrls: ['./cleanup-confirmation-dialog.component.scss']
})
export class CleanupConfirmationDialogComponent {
  constructor(
    public dialogRef: MatDialogRef<CleanupConfirmationDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: CleanupConfirmationData
  ) {}

  onCancel(): void {
    this.dialogRef.close(false);
  }

  onConfirm(): void {
    this.dialogRef.close(true);
  }

  formatDate(dateString: string): string {
    return new Date(dateString).toLocaleDateString('he-IL', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    });
  }
}
