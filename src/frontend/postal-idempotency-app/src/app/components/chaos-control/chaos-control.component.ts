import {
  Component,
  signal,
  computed,
  effect,
  ViewChild,
  ElementRef,
  AfterViewInit,
  OnDestroy,
  inject,
} from "@angular/core";
import { CommonModule, DatePipe } from "@angular/common";
import { toSignal } from "@angular/core/rxjs-interop";
import { ReactiveFormsModule } from "@angular/forms";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import {
  interval,
  switchMap,
  timer,
  map,
  startWith,
  Subject,
  takeUntil,
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
export class ChaosControlComponent implements AfterViewInit, OnDestroy {
  @ViewChild("terminalContent") private terminalContentRef!: ElementRef;
  private destroy$ = new Subject<void>();
  private saveTimeout?: number;

  // Inject services using modern Angular 20 approach
  chaosService = inject(ChaosService); // Made public for template access
  private metricsService = inject(MetricsService);

  // --- State Signals (Angular 20 style) ---
  idempotencyProtectionEnabled = signal(true);
  expirationHours = signal(24);
  private initialSettings = signal<Partial<ChaosSettings>>({});
  metrics = signal<MetricsSummary | null>(null);
  logHistory = signal<LogEntry[]>([]);

  // --- Derived Signals using computed ---
  isProtectionActive = computed(() => this.idempotencyProtectionEnabled());
  
  currentTime = toSignal(
    timer(0, 1000).pipe(map(() => new Date())),
    { initialValue: new Date() }
  );

  settingsChanged = computed(() => {
    const initial = this.initialSettings();
    return (
      this.idempotencyProtectionEnabled() !== initial.useIdempotencyKey ||
      this.expirationHours() !== initial.idempotencyExpirationHours
    );
  });

  constructor() {
    // Effect to scroll terminal down when new logs are added
    effect(() => {
      if (this.logHistory().length && this.terminalContentRef) {
        this.scrollToBottom();
      }
    });

    // Initialize the component
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
    
    // Clear any pending save timeout
    if (this.saveTimeout) {
      clearTimeout(this.saveTimeout);
    }
  }

  private loadInitialSettings() {
    // טעינת הגדרות נוכחיות מהשירות
    const currentSettings = this.chaosService.settings();
    const focusedSettings = {
      useIdempotencyKey: currentSettings.useIdempotencyKey,
      idempotencyExpirationHours: currentSettings.idempotencyExpirationHours,
    };

    this.initialSettings.set(focusedSettings);
    this.idempotencyProtectionEnabled.set(currentSettings.useIdempotencyKey);
    this.expirationHours.set(currentSettings.idempotencyExpirationHours);
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
      useIdempotencyKey: this.idempotencyProtectionEnabled(),
      idempotencyExpirationHours: this.expirationHours(),
    };

    this.addLog("info", "שומר הגדרות חדשות...");

    // Create a simplified settings object with only the 2 supported settings
    const completeSettings: ChaosSettings = {
      useIdempotencyKey: newSettings.useIdempotencyKey!,
      idempotencyExpirationHours: newSettings.idempotencyExpirationHours!,
    };

    // Update settings on server via HTTP call
    this.chaosService.updateSettingsOnServer(completeSettings)
    .pipe(takeUntil(this.destroy$))
    .subscribe({
      next: () => {
        // Update local state
        this.chaosService.updateSettings(newSettings);
        this.initialSettings.set(newSettings);
        this.addLog("success", "ההגדרות נשמרו בהצלחה בשרת");
      },
      error: (error) => {
        console.error("Failed to save settings:", error);
        this.addLog("error", "שגיאה בשמירת ההגדרות בשרת");
        // Revert to previous values
        this.resetSettings();
      }
    });
  }

  resetSettings() {
    const initial = this.initialSettings();
    this.idempotencyProtectionEnabled.set(initial.useIdempotencyKey ?? true);
    this.expirationHours.set(initial.idempotencyExpirationHours ?? 24);
    this.addLog("warn", "השינויים בוטלו - חזרה להגדרות הקודמות");
  }

  // Methods for template binding
  toggleIdempotencyProtection() {
    this.idempotencyProtectionEnabled.update(current => !current);
    // Auto-save when user toggles
    this.saveSettings();
  }

  updateExpirationHours(hours: number) {
    this.expirationHours.set(hours);
    
    // Clear existing timeout
    if (this.saveTimeout) {
      clearTimeout(this.saveTimeout);
    }
    
    // Set new timeout for auto-save
    this.saveTimeout = window.setTimeout(() => this.saveSettings(), 1000);
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
