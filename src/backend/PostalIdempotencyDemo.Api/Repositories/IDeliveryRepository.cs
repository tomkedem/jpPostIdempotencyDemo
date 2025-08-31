namespace PostalIdempotencyDemo.Api.Repositories
{
    public interface IDeliveryRepository
    {
        Task CreateDeliveryAsync(object delivery);
        Task<Models.Shipment?> UpdateDeliveryStatusAsync(string barcode, int statusId);
        Task<Models.Shipment?> GetShipmentByBarcodeAsync(string barcode);
    }

    // ...existing code...
}
