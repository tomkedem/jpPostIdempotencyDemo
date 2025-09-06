import { Injectable } from "@angular/core";
import { HttpClient } from "@angular/common/http";
import { Observable } from "rxjs";
import { environment } from "../../environments/environment";

export interface MetricsSummary {
  totalOperations: number;
  successfulOperations: number;
  idempotentHits: number;
  failedOperations: number;
  averageExecutionTimeMs: number;
}

@Injectable({
  providedIn: "root",
})
export class MetricsService {
  private apiUrl = `${environment.apiUrl}/metrics`;

  constructor(private http: HttpClient) {}

  getMetricsSummary(): Observable<MetricsSummary> {
    return this.http.get<MetricsSummary>(`${this.apiUrl}/summary`);
  }
}
