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
export class ChaosControlComponent implements AfterViewInit, OnDestroy {
  @ViewChild("terminalContent") private terminalContentRef!: ElementRef;
  @ViewChild("performanceChart")
  private chartRef!: ElementRef<HTMLCanvasElement>;
  private destroy$ = new Subject<void>();
  private saveTimeout?: number;
  private chartContext?: CanvasRenderingContext2D;
  private performanceData: {
    timestamp: number;
    successful: number;
    blocked: number;
    chaosErrors: number; // NEW: שגיאות כאשר הגנה כבויה
  }[] = [];

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

  currentTime = toSignal(timer(0, 1000).pipe(map(() => new Date())), {
    initialValue: new Date(),
  });

  settingsChanged = computed(() => {
    const initial = this.initialSettings();
    return (
      this.idempotencyProtectionEnabled() !== initial.useIdempotencyKey ||
      this.expirationHours() !== initial.idempotencyExpirationHours
    );
  });

  systemHealthStatus = computed(() => {
    const m = this.metrics();
    if (!m) return "unknown";

    const successRate =
      m.totalOperations > 0
        ? (m.successfulOperations / m.totalOperations) * 100
        : 100;
    const avgResponseTime = m.averageResponseTime || 0;

    if (successRate >= 98 && avgResponseTime < 200) return "excellent";
    if (successRate >= 95 && avgResponseTime < 500) return "good";
    if (successRate >= 90 && avgResponseTime < 1000) return "fair";
    return "poor";
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
    this.initializeChart();
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
          this.updatePerformanceData(metrics);
        },
        error: (error) => {
          console.error("שגיאה בטעינת מדדים:", error);
          this.addLog("error", "שגיאה בטעינת מדדים");
        },
      });
  }

  private updatePerformanceData(metrics: MetricsSummary) {
    const newPoint = {
      timestamp: Date.now(),
      successful: metrics.successfulOperations,
      blocked: metrics.idempotentBlocks,
      chaosErrors: metrics.chaosDisabledErrors || 0, // NEW: שגיאות כאשר הגנה כבויה
    };

    this.performanceData.push(newPoint);
    this.performanceData = this.performanceData.slice(-10); // Keep last 10 points

    this.drawChart();
  }

  // Methods for template binding
  toggleIdempotencyProtection() {
    this.idempotencyProtectionEnabled.update((current) => !current);
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

  private saveSettings() {
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
    this.chaosService
      .updateSettingsOnServer(completeSettings)
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
        },
      });
  }

  private initializeChart() {
    if (this.chartRef?.nativeElement) {
      const context = this.chartRef.nativeElement.getContext("2d");
      this.chartContext = context || undefined;
      this.drawChart();
    }
  }

  private drawChart() {
    if (!this.chartContext) return;

    const canvas = this.chartRef.nativeElement;
    const ctx = this.chartContext;

    // Clear canvas
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    // Draw grid and axes
    ctx.strokeStyle = "#e2e8f0";
    ctx.lineWidth = 1;

    // Vertical grid lines
    for (let i = 0; i <= 10; i++) {
      const x = (canvas.width / 10) * i;
      ctx.beginPath();
      ctx.moveTo(x, 0);
      ctx.lineTo(x, canvas.height);
      ctx.stroke();
    }

    // Horizontal grid lines
    for (let i = 0; i <= 5; i++) {
      const y = (canvas.height / 5) * i;
      ctx.beginPath();
      ctx.moveTo(0, y);
      ctx.lineTo(canvas.width, y);
      ctx.stroke();
    }

    // Draw performance data if available
    if (this.performanceData.length > 1) {
      this.drawPerformanceLine(ctx, canvas);
    }
  }

  private drawPerformanceLine(
    ctx: CanvasRenderingContext2D,
    canvas: HTMLCanvasElement
  ) {
    const maxValue =
      Math.max(
        ...this.performanceData.map((d) => Math.max(d.successful, d.blocked, d.chaosErrors))
      ) || 10;

    // Draw successful operations line
    ctx.strokeStyle = "#22c55e"; // ירוק - פעולות מוצלחות
    ctx.lineWidth = 2;
    ctx.beginPath();

    this.performanceData.forEach((point, index) => {
      const x = (canvas.width / (this.performanceData.length - 1)) * index;
      const y = canvas.height - (point.successful / maxValue) * canvas.height;

      if (index === 0) {
        ctx.moveTo(x, y);
      } else {
        ctx.lineTo(x, y);
      }
    });
    ctx.stroke();

    // Draw blocked operations line
    ctx.strokeStyle = "#ef4444"; // אדום - פעולות חסומות (הגנה עבדה)
    ctx.lineWidth = 2;
    ctx.beginPath();

    this.performanceData.forEach((point, index) => {
      const x = (canvas.width / (this.performanceData.length - 1)) * index;
      const y = canvas.height - (point.blocked / maxValue) * canvas.height;

      if (index === 0) {
        ctx.moveTo(x, y);
      } else {
        ctx.lineTo(x, y);
      }
    });
    ctx.stroke();

    // Draw chaos disabled errors line
    ctx.strokeStyle = "#f97316"; // כתום - שגיאות כאשר הגנה כבויה
    ctx.lineWidth = 2;
    ctx.beginPath();

    this.performanceData.forEach((point, index) => {
      const x = (canvas.width / (this.performanceData.length - 1)) * index;
      const y = canvas.height - (point.chaosErrors / maxValue) * canvas.height;

      if (index === 0) {
        ctx.moveTo(x, y);
      } else {
        ctx.lineTo(x, y);
      }
    });
    ctx.stroke();
  }

  // Advanced functionality methods
  resetMetrics() {
    if (confirm("האם אתה בטוח שברצונך לאפס את כל המדדים?")) {
      this.metricsService.resetMetrics().subscribe({
        next: () => {
          this.performanceData = [];
          this.drawChart();
          this.addLog("success", "המדדים אופסו בהצלחה");
        },
        error: (error) => {
          console.error("Error resetting metrics:", error);
          this.addLog("error", "שגיאה באיפוס המדדים");
        },
      });
    }
  }

  exportMetrics() {
    const currentMetrics = this.metrics();
    if (!currentMetrics) {
      this.addLog("warn", "אין מדדים לייצוא");
      return;
    }

    const reportData = {
      timestamp: new Date().toISOString(),
      metrics: currentMetrics,
      performanceHistory: this.performanceData,
      systemHealth: this.systemHealthStatus(),
      settings: {
        idempotencyEnabled: this.idempotencyProtectionEnabled(),
        expirationHours: this.expirationHours(),
      },
    };

    const blob = new Blob([JSON.stringify(reportData, null, 2)], {
      type: "application/json",
    });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `idempotency-report-${
      new Date().toISOString().split("T")[0]
    }.json`;
    link.click();
    window.URL.revokeObjectURL(url);

    this.addLog("success", "דוח המדדים יוצא בהצלחה");
  }

  // Helper methods for system health indicator
  getSystemHealthClass(): string {
    const status = this.systemHealthStatus();
    return `health-${status}`;
  }

  getSystemHealthIcon(): string {
    const status = this.systemHealthStatus();
    switch (status) {
      case "excellent":
        return "check_circle";
      case "good":
        return "thumb_up";
      case "fair":
        return "warning";
      case "poor":
        return "error";
      default:
        return "help";
    }
  }

  getSystemHealthText(): string {
    const status = this.systemHealthStatus();
    switch (status) {
      case "excellent":
        return "מצוין";
      case "good":
        return "טוב";
      case "fair":
        return "בסדר";
      case "poor":
        return "בעייתי";
      default:
        return "לא ידוע";
    }
  }

  getBlockedPercentage(): number {
    const metrics = this.metrics();
    if (!metrics || metrics.totalOperations === 0) return 0;
    return (metrics.idempotentBlocks / metrics.totalOperations) * 100;
  }

  getSuccessPercentage(): number {
    const metrics = this.metrics();
    if (!metrics || metrics.totalOperations === 0) return 0;
    return (metrics.successfulOperations / metrics.totalOperations) * 100;
  }

  getChaosErrorsPercentage(): number {
    const metrics = this.metrics();
    if (!metrics || metrics.totalOperations === 0) return 0;
    return ((metrics.chaosDisabledErrors || 0) / metrics.totalOperations) * 100;
  }

  getChaosErrorsCount(): number {
    const metrics = this.metrics();
    return metrics?.chaosDisabledErrors || 0;
  }

  getRecentLogs(): LogEntry[] {
    const logs = this.logHistory();
    // הצגת 10 הרשומות האחרונות בלבד
    return logs.slice(-10).reverse();
  }

  clearLogs(): void {
    this.logHistory.set([]);
    this.addLog("info", "לוג נוקה על ידי המשתמש");
  }
}
