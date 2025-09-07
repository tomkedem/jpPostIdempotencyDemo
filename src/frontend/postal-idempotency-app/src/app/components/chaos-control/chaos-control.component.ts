import {
  Component,
  OnInit,
  OnDestroy,
  signal,
  computed,
  effect,
  ViewChild,
  ElementRef,
  AfterViewInit,
} from "@angular/core";
import { CommonModule, DatePipe } from "@angular/common";
import { FormControl, ReactiveFormsModule } from "@angular/forms";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import {
  Subject,
  takeUntil,
  interval,
  switchMap,
  timer,
  map,
  startWith,
  Observable,
} from "rxjs";

import { ChaosService, ChaosSettings } from "../../services/chaos.service";
import { MetricsService, MetricsSummary } from "../../services/metrics.service";

interface LogEntry {
  timestamp: number;
  level: "info" | "success" | "warn" | "error";
  message: string;
}

@Component({
  selector: "app-chaos-control",
  standalone: true,
  imports: [
    CommonModule,
    DatePipe,
    ReactiveFormsModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
  ],
  templateUrl: "./chaos-control.component.html",
  styleUrls: ["./chaos-control.component.scss"],
})
export class ChaosControlComponent implements OnInit, OnDestroy, AfterViewInit {
  @ViewChild("terminalContent") private terminalContentRef!: ElementRef;
  private destroy$ = new Subject<void>();

  // --- Form Controls ---
  idempotencyProtectionControl = new FormControl(true, { nonNullable: true });
  expirationHoursControl = new FormControl(24, { nonNullable: true });

  // --- State Signals ---
  private initialSettings = signal<Partial<ChaosSettings>>({});
  metrics = signal<MetricsSummary | null>(null);
  logHistory = signal<LogEntry[]>([]);

  // --- Derived Properties ---
  isProtectionActive$: Observable<boolean> =
    this.idempotencyProtectionControl.valueChanges.pipe(
      startWith(this.idempotencyProtectionControl.value)
    );

  currentTime$: Observable<Date> = timer(0, 1000).pipe(map(() => new Date()));

  settingsChanged = computed(() => {
    const initial = this.initialSettings();
    return (
      this.idempotencyProtectionControl.value !== initial.useIdempotencyKey ||
      this.expirationHoursControl.value !== initial.idempotencyExpirationHours
    );
  });

  constructor(
    private chaosService: ChaosService,
    private metricsService: MetricsService
  ) {
    // Effect to scroll terminal down when new logs are added
    effect(() => {
      if (this.logHistory().length && this.terminalContentRef) {
        this.scrollToBottom();
      }
    });
  }

  ngOnInit() {
    this.loadInitialSettings();
    this.startRealTimeUpdates();
    this.addLog("info", "מערכת בקרת אידמפוטנטיות הופעלה");
  }

  ngAfterViewInit() {
    setTimeout(() => this.scrollToBottom(), 100);
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadInitialSettings() {
    // טעינת הגדרות נוכחיות מהשירות
    const currentSettings = this.chaosService.settings();
    const focusedSettings = {
      useIdempotencyKey: currentSettings.useIdempotencyKey,
      idempotencyExpirationHours: currentSettings.idempotencyExpirationHours,
    };

    this.initialSettings.set(focusedSettings);
    this.idempotencyProtectionControl.setValue(
      currentSettings.useIdempotencyKey,
      { emitEvent: false }
    );
    this.expirationHoursControl.setValue(
      currentSettings.idempotencyExpirationHours,
      { emitEvent: false }
    );
    this.addLog("info", "הגדרות נוכחיות נטענו בהצלחה");
  }

  private startRealTimeUpdates() {
    // עדכון מדדים כל 3 שניות
    interval(3000)
      .pipe(
        startWith(0),
        switchMap(() => this.metricsService.getMetricsSummary()),
        takeUntil(this.destroy$)
      )
      .subscribe({
        next: (metrics) => {
          this.metrics.set(metrics);
        },
        error: (error) => {
          console.error("שגיאה בטעינת מדדים:", error);
          this.addLog("error", "שגיאה בטעינת מדדים");
        },
      });
  }

  saveSettings() {
    const newSettings: Partial<ChaosSettings> = {
      useIdempotencyKey: this.idempotencyProtectionControl.value,
      idempotencyExpirationHours: this.expirationHoursControl.value,
    };

    this.addLog("info", "שומר הגדרות חדשות...");

    // עדכון הגדרות בשירות
    this.chaosService.updateSettings(newSettings);

    // עדכון ההגדרות הראשוניות
    this.initialSettings.set(newSettings);
    this.addLog("success", "ההגדרות נשמרו בהצלחה");
  }

  resetSettings() {
    const initial = this.initialSettings();
    this.idempotencyProtectionControl.setValue(
      initial.useIdempotencyKey ?? true
    );
    this.expirationHoursControl.setValue(
      initial.idempotencyExpirationHours ?? 24
    );
    this.addLog("warn", "השינויים בוטלו - חזרה להגדרות הקודמות");
  }

  private addLog(level: LogEntry["level"], message: string) {
    const newEntry: LogEntry = {
      timestamp: Date.now(),
      level,
      message,
    };
    this.logHistory.update((currentLogs) =>
      [...currentLogs, newEntry].slice(-50)
    ); // שמירת 50 הרשומות האחרונות
  }

  private scrollToBottom(): void {
    try {
      if (this.terminalContentRef?.nativeElement) {
        this.terminalContentRef.nativeElement.scrollTop =
          this.terminalContentRef.nativeElement.scrollHeight;
      }
    } catch (err) {
      // שגיאה שקטה - לא חשוב אם הגלילה נכשלת
    }
  }
}
