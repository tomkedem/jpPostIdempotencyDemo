import { bootstrapApplication } from '@angular/platform-browser';
import { importProvidersFrom } from '@angular/core';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideZonelessChangeDetection } from '@angular/core';
import { AppComponent } from './app/app.component';

// Define routes with proper typing
const routes = [
  { path: '', redirectTo: '/create-shipment', pathMatch: 'full' as const },
  { path: 'create-shipment', loadComponent: () => import('./app/components/create-shipment/create-shipment.component').then(m => m.CreateShipmentComponent) },
  { path: 'lookup', loadComponent: () => import('./app/components/shipment-lookup/shipment-lookup.component').then(m => m.ShipmentLookupComponent) },
  { path: 'chaos-control', loadComponent: () => import('./app/components/chaos-control/chaos-control.component').then(m => m.ChaosControlComponent) },
  { path: '**', redirectTo: '/create-shipment' }
];

bootstrapApplication(AppComponent, {
  providers: [
    provideZonelessChangeDetection(),
    importProvidersFrom(BrowserAnimationsModule),
    importProvidersFrom(HttpClientModule),
    provideRouter(routes)
  ]
}).catch(err => console.error(err));
