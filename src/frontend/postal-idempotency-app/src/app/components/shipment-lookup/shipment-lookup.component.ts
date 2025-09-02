import { Component, OnInit, signal } from "@angular/core";
import {
  FormBuilder,
  FormGroup,
  Validators,
  ReactiveFormsModule,
} from "@angular/forms";
import { MatSnackBar, MatSnackBarModule } from "@angular/material/snack-bar";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatInputModule } from "@angular/material/input";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatChipsModule } from "@angular/material/chips";
import { CommonModule, DatePipe } from "@angular/common";
import { ShipmentService } from "../../services/shipment.service";
import { Shipment } from "../../models/shipment.model";

@Component({
  selector: "app-shipment-lookup",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCardModule,
    MatIconModule,
    MatChipsModule,
    MatSnackBarModule,
    DatePipe,
  ],
  templateUrl: "./shipment-lookup.component.html",
  styleUrls: ["./shipment-lookup.component.scss"],
})
export class ShipmentLookupComponent implements OnInit {
  lookupForm: FormGroup;
  isLoading = signal(false);
  shipment = signal<Shipment | null>(null);
  delivery = signal<import("../../models/delivery.model").Delivery | null>(
    null
  );
  lastSearchedBarcode: string | null = null;

  private shipmentStatuses: Map<number, string> = new Map([
    [1, "נוצר"],
    [2, "נמסר"],
    [3, "נכשל"],
    [4, "נמסר חלקית"],
    [5, "בדרך"],
    [6, "בדרך לחלוקה"],
    [7, "חריגה"],
  ]);

  constructor(
    private fb: FormBuilder,
    private shipmentService: ShipmentService,
    private snackBar: MatSnackBar
  ) {
    this.lookupForm = this.fb.group({
      barcode: [
        "",
        [Validators.required, Validators.pattern(/^[A-Za-z0-9]{13}$/)],
      ],
    });
  }

  ngOnInit(): void {}

  async onLookup(): Promise<void> {
    if (this.lookupForm.valid && !this.isLoading()) {
      this.isLoading.set(true);
      this.shipment.set(null);
      this.delivery.set(null);

      const barcode = this.lookupForm.value.barcode;
      this.lastSearchedBarcode = barcode;

      try {
        const result =
          await this.shipmentService.getShipmentAndDeliveryByBarcode(barcode);
        console.log("Shipment found:", result.shipment);
        console.log("Delivery found:", result.delivery);
        this.shipment.set(result.shipment);
        this.delivery.set(result.delivery);
      } catch (error: any) {
        this.snackBar.open(`שגיאה: ${error.message}`, "סגור", {
          duration: 5000,
          panelClass: ["error-snackbar"],
        });
      } finally {
        this.isLoading.set(false);
      }
      console.log("Last delivery:", this.delivery());
    }
  }

  async onCancel(): Promise<void> {
    if (this.shipment() && !this.isLoading()) {
      this.isLoading.set(true);

      try {
        await this.shipmentService.cancelShipment(this.shipment()!.barcode);
        this.snackBar.open("משלוח בוטל בהצלחה!", "סגור", {
          duration: 3000,
          panelClass: ["success-snackbar"],
        });
        // Refresh shipment data
        await this.onLookup();
      } catch (error: any) {
        this.snackBar.open(`שגיאה בביטול: ${error.message}`, "סגור", {
          duration: 5000,
          panelClass: ["error-snackbar"],
        });
      } finally {
        this.isLoading.set(false);
      }
    }
  }

  async onUpdateStatus(barcode: string, statusId: number) {
    this.isLoading.set(true);
    try {
      const response = await this.shipmentService.updateDeliveryStatus(
        barcode,
        statusId
      );
      if (response.success && response.data) {
        // עדכון אמיתי
        const statusText =
          response.data.statusNameHe ||
          this.getStatusText(response.data.status);
        this.snackBar.open(
          `סטטוס משלוח עודכן בהצלחה ל: ${statusText}`,
          "סגור",
          { duration: 3000 }
        );
        // רענון מלא של המשלוח מהשרת כדי להציג תאריך עדכון
        const result =
          await this.shipmentService.getShipmentAndDeliveryByBarcode(barcode);
        this.shipment.set(result.shipment);
        this.delivery.set(result.delivery);
      } else if (response.success && !response.data) {
        // חסימה אידמפונטנטית
        this.snackBar.open(
          "העדכון נחסם בגלל מפתח אידמפונטנטי, סטטוס לא שונה.",
          "סגור",
          { duration: 4000 }
        );
      } else {
        // שגיאה
        const errorMessage = response.message || "אירעה שגיאה בעדכון הסטטוס";
        this.snackBar.open(errorMessage, "סגור", {
          duration: 5000,
          direction: "rtl",
        });
      }
    } catch (error: any) {
      const errorMessage = error.error?.message || "אירעה שגיאה בעדכון הסטטוס";
      this.snackBar.open(errorMessage, "סגור", {
        duration: 5000,
        direction: "rtl",
      });
    } finally {
      this.isLoading.set(false);
    }
    console.log("Last delivery on status update:", this.delivery());
  }

  getStatusText(statusId: number): string {
    console.log("Getting status text for ID:", statusId);
    return this.shipmentStatuses.get(statusId) || "לא עודכן";
  }

  getStatusColor(status: number): string {
    switch (status) {
      case 1:
        return "green";
      case 2:
        return "red";
      case 3:
        return "orange";
      default:
        return "gray";
    }
  }
}
