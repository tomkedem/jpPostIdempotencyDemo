import { Component, OnInit, OnDestroy, signal, computed } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormControl, ReactiveFormsModule } from "@angular/forms";
import { MatSlideToggleModule } from "@angular/material/slide-toggle";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { Subject, takeUntil, interval, switchMap } from "rxjs";

import { ChaosService, ChaosSettings } from "../../services/chaos.service";
import {
  MetricsService,
  RealTimeMetrics,
  SystemHealth,
} from "../../services/metrics.service";

@Component({
  selector: "app-chaos-control",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: "./chaos-control.component.html",
  styleUrls: ["./chaos-control.component.scss"],
})
export class ChaosControlComponent implements OnInit, OnDestroy {
  private destroy$ = new Subject<void>();

  // Form Controls for all system settings
  idempotencyProtectionControl = new FormControl(true);
  forceErrorControl = new FormControl(false);
  expirationHoursControl = new FormControl(24);
  maxRetriesControl = new FormControl(3);
  timeoutControl = new FormControl(30);
  metricsCollectionControl = new FormControl(true);
  metricsRetentionControl = new FormControl(30);
  chaosModeControl = new FormControl(false);
  maintenanceModeControl = new FormControl(false);

  // Signals for reactive data
  private metricsSignal = signal<RealTimeMetrics | null>(null);
  private systemHealthSignal = signal<SystemHealth | null>(null);

  // Computed signals
  metrics = this.metricsSignal.asReadonly();
  systemHealth = this.systemHealthSignal.asReadonly();

  systemStatus = computed(() => {
    const health = this.systemHealth();
    if (!health) {
      return {
        statusText: "טוען...",
        statusColor: "gray",
        performanceLevel: "לא זמין",
      };
    }

    if (health.status === "healthy") {
      return {
        statusText: "תקין",
        statusColor: "green",
        performanceLevel: "good",
      };
    } else {
      return {
        statusText: "בעיה",
        statusColor: "red",
        performanceLevel: "ירוד",
      };
    }
  });

  // Demo steps for enhanced guide
  demoSteps = [
    {
      icon: "settings",
      text: "<strong>הגדר את המערכת:</strong> התאם את זמן התפוגה, מספר הניסיונות החוזרים וזמן הקצוב לפי הצרכים שלך",
    },
    {
      icon: "security",
      text: "<strong>הפעל הגנה:</strong> הפעל את הגנת האידמפוטנטיות כדי למנוע כפילויות בפעולות קריטיות",
    },
    {
      icon: "send",
      text: "<strong>שלח בקשות:</strong> בצע בקשות POST עם כותרת <code>Idempotency-Key</code> ייחודית",
    },
    {
      icon: "block",
      text: "<strong>צפה בחסימות:</strong> בקשות חוזרות עם אותו מפתח יחסמו אוטומטית",
    },
    {
      icon: "analytics",
      text: "<strong>נטר ביצועים:</strong> עקוב אחר זמני תגובה, אחוזי הצלחה ומדדי מערכת בזמן אמת",
    },
    {
      icon: "tune",
      text: "<strong>התאם הגדרות:</strong> שנה פרמטרים מתקדמים כמו מצב כאוס ומצב תחזוקה לפי הצורך",
    },
  ];

  constructor(
    private chaosService: ChaosService,
    private metricsService: MetricsService
  ) {}

  ngOnInit() {
    this.initializeFormControls();
    this.startRealTimeUpdates();
  }

  ngOnDestroy() {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private initializeFormControls() {
    // Get current chaos service settings and update form controls
    const settings = this.chaosService.settings();
    this.idempotencyProtectionControl.setValue(settings.useIdempotencyKey, {
      emitEvent: false,
    });
    this.forceErrorControl.setValue(settings.forceError, {
      emitEvent: false,
    });
    this.expirationHoursControl.setValue(settings.idempotencyExpirationHours, {
      emitEvent: false,
    });
    this.maxRetriesControl.setValue(settings.maxRetryAttempts, {
      emitEvent: false,
    });
    this.timeoutControl.setValue(settings.defaultTimeoutSeconds, {
      emitEvent: false,
    });
    this.metricsCollectionControl.setValue(settings.enableMetricsCollection, {
      emitEvent: false,
    });
    this.metricsRetentionControl.setValue(settings.metricsRetentionDays, {
      emitEvent: false,
    });
    this.chaosModeControl.setValue(settings.enableChaosMode, {
      emitEvent: false,
    });
    this.maintenanceModeControl.setValue(settings.systemMaintenanceMode, {
      emitEvent: false,
    });
  }

  private startRealTimeUpdates() {
    // Real-time metrics updates every 2 seconds
    interval(2000)
      .pipe(
        switchMap(() => this.metricsService.getRealTimeMetrics()),
        takeUntil(this.destroy$)
      )
      .subscribe((metrics) => {
        this.metricsSignal.set(metrics);
      });

    // System health updates every 5 seconds
    interval(5000)
      .pipe(
        switchMap(() => this.metricsService.getSystemHealth()),
        takeUntil(this.destroy$)
      )
      .subscribe((health) => {
        this.systemHealthSignal.set(health);
      });
  }

  saveSettings() {
    const settings: ChaosSettings = {
      useIdempotencyKey: this.idempotencyProtectionControl.value || false,
      forceError: this.forceErrorControl.value || false,
      idempotencyExpirationHours: this.expirationHoursControl.value || 24,
      maxRetryAttempts: this.maxRetriesControl.value || 3,
      defaultTimeoutSeconds: this.timeoutControl.value || 30,
      enableMetricsCollection: this.metricsCollectionControl.value || true,
      metricsRetentionDays: this.metricsRetentionControl.value || 30,
      enableChaosMode: this.chaosModeControl.value || false,
      systemMaintenanceMode: this.maintenanceModeControl.value || false,
    };

    this.chaosService
      .updateSettingsOnServer(settings)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          console.log("Settings saved successfully");
        },
        error: (error) => {
          console.error("Failed to save settings:", error);
        },
      });
  }

  resetMetrics() {
    this.metricsService
      .resetMetrics()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          console.log("Metrics reset successfully");
          // Refresh metrics immediately
          this.metricsService.getRealTimeMetrics().subscribe((metrics) => {
            this.metricsSignal.set(metrics);
          });
        },
        error: (error) => {
          console.error("Failed to reset metrics:", error);
        },
      });
  }

  resetToDefaults() {
    const defaultSettings: ChaosSettings = {
      useIdempotencyKey: true,
      forceError: false,
      idempotencyExpirationHours: 24,
      maxRetryAttempts: 3,
      defaultTimeoutSeconds: 30,
      enableMetricsCollection: true,
      metricsRetentionDays: 30,
      enableChaosMode: false,
      systemMaintenanceMode: false,
    };

    // Update form controls
    this.idempotencyProtectionControl.setValue(
      defaultSettings.useIdempotencyKey
    );
    this.forceErrorControl.setValue(defaultSettings.forceError);
    this.expirationHoursControl.setValue(
      defaultSettings.idempotencyExpirationHours
    );
    this.maxRetriesControl.setValue(defaultSettings.maxRetryAttempts);
    this.timeoutControl.setValue(defaultSettings.defaultTimeoutSeconds);
    this.metricsCollectionControl.setValue(
      defaultSettings.enableMetricsCollection
    );
    this.metricsRetentionControl.setValue(defaultSettings.metricsRetentionDays);
    this.chaosModeControl.setValue(defaultSettings.enableChaosMode);
    this.maintenanceModeControl.setValue(defaultSettings.systemMaintenanceMode);

    // Save to server
    this.saveSettings();
  }

  // Helper methods for template
  getHealthStatusIcon(): string {
    const health = this.systemHealth();
    if (!health) return "help";
    return health.status === "healthy" ? "check_circle" : "error";
  }

  getResponseTimeColor(responseTime: number): string {
    if (responseTime < 100) return "primary";
    if (responseTime < 500) return "accent";
    return "warn";
  }

  getSystemLoadColor(load: number): string {
    if (load < 50) return "primary";
    if (load < 80) return "accent";
    return "warn";
  }

  getProgressValue(value: number, divisor: number): number {
    return Math.min(value / divisor, 100);
  }

  getCurrentTime(): string {
    return new Date().toLocaleTimeString("he-IL");
  }
}
