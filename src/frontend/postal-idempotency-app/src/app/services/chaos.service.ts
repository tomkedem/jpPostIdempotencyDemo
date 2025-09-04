import { Injectable, signal } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable, catchError, tap, throwError } from "rxjs";

export interface ChaosSettings {
  useIdempotencyKey: boolean;
  forceError: boolean;
}

@Injectable({
  providedIn: "root",
})
export class ChaosService {
  private chaosSettings = signal<ChaosSettings>({
    useIdempotencyKey: true,
    forceError: false,
  });

  // Public signals for components to react to
  settings = this.chaosSettings.asReadonly();

  constructor(private http: HttpClient) {
    this.loadInitialSettings();
  }

  private loadInitialSettings() {
    this.http
      .get<ChaosSettings>("/api/chaos/settings")
      .subscribe((settings) => {
        this.chaosSettings.set(settings);
      });
  }

  updateSettings(useIdempotencyKey: boolean, forceError: boolean): void {
    this.chaosSettings.set({ useIdempotencyKey, forceError });
  }

  updateSettingsOnServer(settings: ChaosSettings): Observable<any> {
    return this.http.post("/api/chaos/settings", settings).pipe(
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
}
