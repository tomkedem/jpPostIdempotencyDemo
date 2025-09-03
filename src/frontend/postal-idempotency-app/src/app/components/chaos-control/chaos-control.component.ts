import { Component, OnInit, signal } from "@angular/core";
import { FormBuilder, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { CommonModule } from "@angular/common";
import { ChaosService } from "../../services/chaos.service";

@Component({
  selector: "app-chaos-control",
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
    MatSnackBarModule,
  ],
  templateUrl: "./chaos-control.component.html",
  styleUrls: ["./chaos-control.component.scss"],
})
export class ChaosControlComponent implements OnInit {
  chaosForm: FormGroup;
  demoSteps: { icon: string; text: string }[] = [
    {
      icon: "looks_one",
      text: `<strong>תרחיש בסיסי (ללא הגנה):</strong> <br> כבו את "הפעל הגנת Idempotency" ונסו ליצור משלוח חדש. לאחר מכן, רעננו את הדף ונסו לשלוח שוב את אותו הטופס. תיווצר כפילות.`,
    },
    {
      icon: "looks_two",
      text: `<strong>הפעלת הגנה:</strong> <br> הפעילו את "הפעל הגנת Idempotency". כל בקשת יצירה כעת מוגנת מכפילויות.`,
    },
    {
      icon: "looks_3",
      text: `<strong>בדיקת הגנה:</strong> <br> הפעילו את "דמה שגיאת רשת". כעת, כשתנסו ליצור משלוח, המערכת תדמה שליחה כפולה. שימו לב שהמשלוח נוצר רק פעם אחת, והמערכת מחזירה את התשובה המקורית במקום ליצור כפילות.`,
    },
  ];

  constructor(
    private fb: FormBuilder,
    private chaosService: ChaosService,
    private snackBar: MatSnackBar
  ) {
    const currentSettings = this.chaosService.settings();
    this.chaosForm = this.fb.group({
      useIdempotencyKey: [currentSettings.useIdempotencyKey],
      forceError: [currentSettings.forceError],
    });
  }

  ngOnInit(): void {
    this.chaosForm.valueChanges.subscribe((values) => {
      this.chaosService.updateSettings(
        values.useIdempotencyKey,
        values.forceError
      );
      this.showSnackbar(values.useIdempotencyKey, values.forceError);
    });
  }

  private showSnackbar(useIdempotencyKey: boolean, forceError: boolean): void {
    let message = "";
    if (!useIdempotencyKey) {
      message = "הגנת Idempotency כבויה. המערכת חשופה לכפילויות.";
    } else {
      if (forceError) {
        message = "סימולציית שגיאת רשת פעילה. המערכת תמנע כפילויות.";
      } else {
        message = "הגנת Idempotency פעילה. המערכת תקינה.";
      }
    }

    this.snackBar.open(message, "סגור", {
      duration: 3500,
      panelClass: ["info-snackbar"],
    });
  }
}
