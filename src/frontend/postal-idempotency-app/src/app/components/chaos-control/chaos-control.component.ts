import { Component, OnInit, signal } from "@angular/core";
import {
  FormBuilder,
  FormGroup,
  FormControl,
  ReactiveFormsModule,
} from "@angular/forms";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatIconModule } from "@angular/material/icon";
import { CommonModule } from "@angular/common";
import { ChaosService } from "../../services/chaos.service";
import { MetricsService, MetricsSummary } from "../../services/metrics.service";
import { CountUpModule } from "ngx-countup";
import {
  debounceTime,
  switchMap,
  catchError,
  EMPTY,
  interval,
  startWith,
} from "rxjs";

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
    CountUpModule,
  ],
})
export class ChaosControlComponent implements OnInit {
  chaosForm: FormGroup;
  idempotencyProtectionControl: FormControl;
  metricsSummary = signal<MetricsSummary | null>(null);

  // Computed signal for metrics with proper structure
  metrics = signal<{
    totalOperations: number;
    successfulOperations: number;
    idempotentBlocks: number;
    averageResponseTime: number;
    successRate: number;
    failedOperations: number;
  } | null>(null);

  demoSteps: { icon: string; text: string }[] = [
    {
      icon: "looks_one",
      text: `<strong>תרחיש בסיסי (ללא הגנה):</strong> <br> כבו את \"הפעל הגנת Idempotency\" ונסו לעדכן סטטוס פעמיים. שימו לב בלוח המחוונים שהפעולה מתבצעת פעמיים.`,
    },
    {
      icon: "looks_two",
      text: `<strong>הפעלת הגנה:</strong> <br> הפעילו את \"הפעל הגנת Idempotency\". נסו לעדכן את אותו הסטטוס שוב. שימו לב שהפעולה נחסמת ונספרת בלוח המחוונים.`,
    },
  ];

  constructor(
    private fb: FormBuilder,
    private chaosService: ChaosService,
    private metricsService: MetricsService,
    private snackBar: MatSnackBar
  ) {
    this.idempotencyProtectionControl = new FormControl(true);
    this.chaosForm = this.fb.group({
      useIdempotencyKey: this.idempotencyProtectionControl,
    });
  }

  ngOnInit(): void {
    // Fetch metrics periodically
    interval(5000)
      .pipe(
        startWith(0),
        switchMap(() => this.metricsService.getMetricsSummary()),
        catchError((err) => {
          console.error("Failed to fetch metrics:", err);
          return EMPTY;
        })
      )
      .subscribe((summary) => {
        this.metricsSummary.set(summary);

        // Update metrics signal with calculated values
        if (summary) {
          const successfulOps =
            summary.totalOperations - summary.failedOperations;
          const successRate =
            summary.totalOperations > 0
              ? Math.round((successfulOps / summary.totalOperations) * 100)
              : 0;

          this.metrics.set({
            totalOperations: summary.totalOperations,
            successfulOperations: successfulOps,
            idempotentBlocks: summary.idempotentHits,
            averageResponseTime: Math.round(summary.averageExecutionTimeMs),
            successRate: successRate,
            failedOperations: summary.failedOperations,
          });
        }
      });

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
              this.chaosForm.patchValue(this.chaosService.settings(), {
                emitEvent: false,
              });
              return EMPTY;
            })
          );
        })
      )
      .subscribe((values) => {
        this.showSnackbar(this.chaosService.settings().useIdempotencyKey);
      });
  }

  showSnackbar(useIdempotencyKey: boolean): void {
    let message: string;
    if (!useIdempotencyKey) {
      message = "הגנת Idempotency כבויה. המערכת חשופה לכפילויות.";
    } else {
      message = "הגנת Idempotency פעילה. המערכת תקינה.";
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
