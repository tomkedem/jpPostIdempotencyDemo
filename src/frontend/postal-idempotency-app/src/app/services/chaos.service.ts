import { Injectable, signal } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable, catchError, tap, throwError } from "rxjs";
import { environment } from "../../environments/environment";

export interface ChaosSettings {
  useIdempotencyKey: boolean;
  forceError: boolean;
  idempotencyExpirationHours: number;
  maxRetryAttempts: number;
  defaultTimeoutSeconds: number;
  enableMetricsCollection: boolean;
  metricsRetentionDays: number;
  enableChaosMode: boolean;
  systemMaintenanceMode: boolean;
}

@Injectable({
  providedIn: "root",
})
export class ChaosService {
  private chaosSettings = signal<ChaosSettings>({
    useIdempotencyKey: true,
    forceError: false,
    idempotencyExpirationHours: 24,
    maxRetryAttempts: 3,
    defaultTimeoutSeconds: 30,
    enableMetricsCollection: true,
    metricsRetentionDays: 30,
    enableChaosMode: false,
    systemMaintenanceMode: false,
  });

  // Public signals for components to react to
  settings = this.chaosSettings.asReadonly();

  constructor(private http: HttpClient) {
    this.loadInitialSettings();
  }

  private loadInitialSettings() {
    this.http
      .get<ChaosSettings>(`${environment.apiUrl}/chaos/settings`)
      .subscribe((settings) => {
        this.chaosSettings.set(settings);
      });
  }

  updateSettings(settings: Partial<ChaosSettings>): void {
    this.chaosSettings.update((current) => ({ ...current, ...settings }));
  }

  updateSettingsOnServer(settings: ChaosSettings): Observable<any> {
    return this.http
      .post(`${environment.apiUrl}/chaos/settings`, settings)
      .pipe(
        tap(() => {
          this.chaosSettings.set(settings);
        }),
        catchError((err) => {
          console.error("Failed to update chaos settings", err);
          // Optionally, revert the local state if the server call fails
          this.loadInitialSettings();
          return throwError(
            () => new Error("Failed to update settings on server")
          );
        })
      );
  }

  /**
   * Generates a UUID v4 for the idempotency key if chaos is enabled.
   * Returns an empty string otherwise, so no key is sent.
   * @param data The data to be sent in the request body.
   */
  async generateIdempotencyKey(data: string): Promise<string> {
    const { useIdempotencyKey, forceError } = this.chaosSettings();

    if (!useIdempotencyKey) {
      return ""; // Don't send a key if the feature is disabled
    }

    if (forceError) {
      // Send a deliberately malformed key to test server-side validation
      return "force-error-guid";
    }

    // Standard case: generate a valid UUID v4
    return self.crypto.randomUUID();
  }

  /**
   * Get the current idempotency expiration time in hours
   */
  getIdempotencyExpirationHours(): number {
    return this.chaosSettings().idempotencyExpirationHours;
  }

  /**
   * Check if system is in maintenance mode
   */
  isMaintenanceMode(): boolean {
    return this.chaosSettings().systemMaintenanceMode;
  }

  /**
   * Check if chaos mode is enabled
   */
  isChaosMode(): boolean {
    return this.chaosSettings().enableChaosMode;
  }
}
