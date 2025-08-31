import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse, HttpHeaders, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import { Shipment, CreateDeliveryRequest, UpdateStatusRequest, ShipmentStatus, IdempotencyDemoResponse } from '../models/shipment.model';
import { ChaosService } from './chaos.service';

@Injectable({
  providedIn: 'root'
})
export class ShipmentService {
  private readonly apiUrl = 'http://localhost:5000/api/idempotency-demo';
  
  // Signals for state management
  private _loading = signal(false);
  private _error = signal<string | null>(null);
  private _lastResponse = signal<any>(null);
  
  // Public readonly signals
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly lastResponse = this._lastResponse.asReadonly();

  constructor(private http: HttpClient, private chaosService: ChaosService) {}

  async createDelivery(request: CreateDeliveryRequest): Promise<IdempotencyDemoResponse<any>> {
    this._loading.set(true);
    this._error.set(null);
    
    try {

      const response = await firstValueFrom(this.http.post<IdempotencyDemoResponse<any>>(`${this.apiUrl}/idempotency-demo/delivery`, request));
      this._lastResponse.set(response);
      return response;
    } catch (error) {
      const errorMessage = this.handleError(error);
      this._error.set(errorMessage);
      throw new Error(errorMessage);
    } finally {
      this._loading.set(false);
    }
  }

  async createShipment(request: CreateDeliveryRequest): Promise<any> {
    // Redirect to createDelivery for now
    return this.createDelivery(request);
  }

  
  async getShipmentByBarcode(barcode: string): Promise<Shipment> {
    this._loading.set(true);
    this._error.set(null);
    
    try {
      const response = await firstValueFrom(this.http.get<Shipment>(`${environment.apiUrl}/SimpleShipments/${barcode}`));
      this._lastResponse.set(response);
      return response;
    } catch (error) {
      const errorMessage = this.handleError(error);
      this._error.set(errorMessage);
      throw new Error(errorMessage);
    } finally {
      this._loading.set(false);
    }
  }

  async cancelShipment(barcode: string): Promise<any> {
    this._loading.set(true);
    this._error.set(null);
    
    try {
      const response = await firstValueFrom(this.http.post<any>(`${environment.apiUrl}/shipments/${barcode}/cancel`, {}));
      this._lastResponse.set(response);
      return response;
    } catch (error) {
      const errorMessage = this.handleError(error);
      this._error.set(errorMessage);
      throw new Error(errorMessage);
    } finally {
      this._loading.set(false);
    }
  }

  async updateDeliveryStatus(barcode: string, statusId: number): Promise<IdempotencyDemoResponse<Shipment>> {
    console.log('Updating delivery status for barcode:', barcode);
    const url = `${this.apiUrl}/delivery/${barcode}/status`;
    const body = { statusId };
    console.log('Updating delivery status with body:', body);
    const idempotencyKey = await this.chaosService.generateIdempotencyKey(JSON.stringify(body));
    console.log('Generated Idempotency Key:', idempotencyKey);
    const headers = new HttpHeaders({ 'Idempotency-Key': idempotencyKey });

    const response$ = this.http.patch<IdempotencyDemoResponse<Shipment>>(url, body, { headers });
    return await firstValueFrom(response$);
  }

  async enableChaos(enabled: boolean, delayMin: number = 100, delayMax: number = 2000, failureRate: number = 0.1): Promise<any> {
    this._loading.set(true);
    this._error.set(null);
    
    try {
      let params = new HttpParams();
      params = params.set('chaos', enabled.toString());
      if (enabled) {
        params = params.set('chaos-delay-min', delayMin.toString());
        params = params.set('chaos-delay-max', delayMax.toString());
        params = params.set('chaos-failure-rate', failureRate.toString());
      }
      
      const response = await firstValueFrom(this.http.post<any>(`${environment.apiUrl}/chaos`, {}, { params }));
      this._lastResponse.set(response);
      return response;
    } catch (error) {
      const errorMessage = this.handleError(error);
      this._error.set(errorMessage);
      throw new Error(errorMessage);
    } finally {
      this._loading.set(false);
    }
  }

  // Test method to verify API connectivity
  async testConnection(): Promise<{ success: boolean; message: string; endpoint: string }> {
    try {
      const testUrl = `${this.apiUrl}/test/connection`;
      const response = await firstValueFrom(
        this.http.get<any>(testUrl, { 
          observe: 'response',
          timeout: 5000 
        })
      );
      
      return {
        success: true,
        message: `API connected successfully. Database: ${response.body?.message || 'Connected'}`,
        endpoint: testUrl
      };
    } catch (error: any) {
      console.error('API Connection Test Failed:', error);
      
      let message = 'API connection failed';
      if (error.status === 0) {
        message = 'Cannot connect to API server. Is the backend running on port 5000?';
      } else if (error.status === 404) {
        message = 'API endpoint not found. Check if the correct routes are configured.';
      } else if (error.status >= 500) {
        message = 'API server error. Check backend logs.';
      } else {
        message = `API error: ${error.message || error.statusText}`;
      }
      
      return {
        success: false,
        message,
        endpoint: `${this.apiUrl}/test/connection`
      };
    }
  }

  // Clear error state
  clearError(): void {
    this._error.set(null);
  }

  private handleError(error: any): string {
    let errorMessage = 'An unknown error occurred';
    
    if (error instanceof HttpErrorResponse) {
      if (error.error instanceof ErrorEvent) {
        // Client-side error
        errorMessage = `Client Error: ${error.error.message}`;
      } else {
        // Server-side error
        if (error.error?.error) {
          errorMessage = error.error.error;
        } else if (error.error?.message) {
          errorMessage = error.error.message;
        } else {
          errorMessage = `Server Error: ${error.status} - ${error.message}`;
        }
      }
    } else if (error instanceof Error) {
      errorMessage = error.message;
    }
    
    console.error('ShipmentService Error:', error);
    return errorMessage;
  }
}
