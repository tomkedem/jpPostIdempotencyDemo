import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { CreateShipmentComponent } from './components/create-shipment/create-shipment.component';
import { ShipmentLookupComponent } from './components/shipment-lookup/shipment-lookup.component';
import { ChaosControlComponent } from './components/chaos-control/chaos-control.component';

const routes: Routes = [
  { path: '', redirectTo: '/create-shipment', pathMatch: 'full' },
  { path: 'create-shipment', component: CreateShipmentComponent },
  { path: 'lookup', component: ShipmentLookupComponent },
  { path: 'chaos-control', component: ChaosControlComponent },
  { path: '**', redirectTo: '/create-shipment' }
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
