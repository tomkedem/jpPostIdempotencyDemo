using PostalIdempotencyDemo.Api.Models;


namespace PostalIdempotencyDemo.Api.Services.Interfaces;

public interface IDeliveryService
{
    Task<IdempotencyDemoResponse<Delivery>> CreateDeliveryAsync(CreateDeliveryRequest request);
    Task<IdempotencyDemoResponse<Shipment>> UpdateDeliveryStatusAsync(string barcode, int statusId);
    Task LogIdempotentHitAsync(string barcode, string idempotencyKey, string endpoint);
    Task<(Shipment? Shipment, Delivery? Delivery)> GetShipmentAndDeliveryByBarcodeAsync(string barcode);
}