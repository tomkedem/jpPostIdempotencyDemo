import { Component, OnInit, signal } from '@angular/core';
import { FormBuilder, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { CommonModule } from '@angular/common';
import { ChaosService } from '../../services/chaos.service';

@Component({
  selector: 'app-chaos-control',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatSlideToggleModule,
    MatSnackBarModule
  ],
  templateUrl: './chaos-control.component.html',
  styleUrls: ['./chaos-control.component.scss']
})
export class ChaosControlComponent implements OnInit {
  chaosForm: FormGroup;

  constructor(
    private fb: FormBuilder,
    private chaosService: ChaosService,
    private snackBar: MatSnackBar
  ) {
    const currentSettings = this.chaosService.settings();
    this.chaosForm = this.fb.group({
      useIdempotencyKey: [currentSettings.useIdempotencyKey],
      forceError: [currentSettings.forceError]
    });
  }

  ngOnInit(): void {
    this.chaosForm.valueChanges.subscribe(values => {
      this.chaosService.updateSettings(values.useIdempotencyKey, values.forceError);
      this.showSnackbar(values.useIdempotencyKey, values.forceError);
    });
  }

  private showSnackbar(useIdempotencyKey: boolean, forceError: boolean): void {
    let message = 'הגדרות כאוס עודכנו: ';
    if (!useIdempotencyKey) {
      message += 'מפתחות Idempotency מבוטלים.';
    } else if (forceError) {
      message += 'שליחת מפתח שגוי פעילה.';
    } else {
      message += 'מפתחות Idempotency תקינים פעילים.';
    }

    this.snackBar.open(message, 'סגור', {
      duration: 3000,
      panelClass: ['info-snackbar']
    });
  }
}
