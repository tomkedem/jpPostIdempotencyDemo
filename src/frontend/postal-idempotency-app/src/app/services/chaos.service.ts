import { Injectable, signal } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ChaosService {
  private chaosSettings = signal({
    useIdempotencyKey: true,
    forceError: false
  });

  // Public signals for components to react to
  settings = this.chaosSettings.asReadonly();

  constructor() {}

  updateSettings(useIdempotencyKey: boolean, forceError: boolean): void {
    this.chaosSettings.set({ useIdempotencyKey, forceError });
  }

  /**
   * Generates a UUID v4 for the idempotency key if chaos is enabled.
   * Returns an empty string otherwise, so no key is sent.
   * @param data The data to be sent in the request body.
   */
  async generateIdempotencyKey(data: string): Promise<string> {
    const { useIdempotencyKey, forceError } = this.chaosSettings();

    if (!useIdempotencyKey) {
      return ''; // Don't send a key if the feature is disabled
    }

    if (forceError) {
      // Send a deliberately malformed key to test server-side validation
      return 'force-error-guid';
    }

    // Standard case: generate a valid UUID v4
    return self.crypto.randomUUID();
  }
}
