import { Component, OnInit } from "@angular/core";
import { FormBuilder, FormGroup, ReactiveFormsModule } from "@angular/forms";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatIconModule } from "@angular/material/icon";
import { CommonModule } from "@angular/common";
import { ChaosService } from "../../services/chaos.service";
import { debounceTime, switchMap, catchError, EMPTY } from "rxjs";
import { signal } from "@angular/core";

@Component({
  selector: "app-chaos-control",
  templateUrl: "./chaos-control.component.html",
  styleUrls: ["./chaos-control.component.scss"],
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatSlideToggleModule,
    MatIconModule,
    MatSnackBarModule,
  ],
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
    this.chaosForm = this.fb.group({
      useIdempotencyKey: [true],
      forceError: [false],
    });
  }

  ngOnInit(): void {
    // Set initial form state from the service
    this.chaosForm.patchValue(this.chaosService.settings(), {
      emitEvent: false,
    });

    this.chaosForm.valueChanges
      .pipe(
        debounceTime(300),
        switchMap((values) => {
          return this.chaosService.updateSettingsOnServer(values).pipe(
            catchError((error) => {
              this.showErrorSnackbar("העדכון נכשל. אנא נסה שוב.");
              // Revert the toggle state visually
              this.chaosForm.patchValue(this.chaosService.settings(), {
                emitEvent: false,
              });
              return EMPTY;
            })
          );
        })
      )
      .subscribe((values) => {
        // On success, the service has already updated the signal
        this.showSnackbar(
          this.chaosService.settings().useIdempotencyKey,
          this.chaosService.settings().forceError
        );
      });
  }

  showSnackbar(useIdempotencyKey: boolean, forceError: boolean): void {
    let message: string;
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
      duration: 3000,
      verticalPosition: "top",
      horizontalPosition: "center",
      panelClass: useIdempotencyKey ? "success-snackbar" : "error-snackbar",
    });
  }

  private showErrorSnackbar(message: string): void {
    this.snackBar.open(message, "סגור", {
      duration: 5000,
      verticalPosition: "top",
      horizontalPosition: "center",
      panelClass: "error-snackbar",
    });
  }
}
