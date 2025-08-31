using PostalIdempotencyDemo.Api.Models;
using PostalIdempotencyDemo.Api.Repositories;
using System.Diagnostics;
using System.Threading.Tasks;

namespace PostalIdempotencyDemo.Api.Services
{
    public interface IDeliveryService
    {
        Task<IdempotencyDemoResponse<Delivery>> CreateDeliveryAsync(CreateDeliveryRequest request);
        Task<IdempotencyDemoResponse<Shipment>> UpdateDeliveryStatusAsync(string barcode, int statusId);
    }

    public class DeliveryService : IDeliveryService
    {
        private readonly IDeliveryRepository _deliveryRepository;
        public DeliveryService(IDeliveryRepository deliveryRepository)
        {
            _deliveryRepository = deliveryRepository;
        }

        public async Task<IdempotencyDemoResponse<Delivery>> CreateDeliveryAsync(CreateDeliveryRequest request)
        {
            var delivery = new Delivery
            {
                Id = Guid.NewGuid(),
                Barcode = request.Barcode,
                EmployeeId = request.EmployeeId,
                DeliveryDate = DateTime.UtcNow,
                LocationLat = request.LocationLat,
                LocationLng = request.LocationLng,
                RecipientName = request.RecipientName,
                StatusId = request.DeliveryStatus,
                Notes = request.Notes,
                CreatedAt = DateTime.UtcNow
            };
            await _deliveryRepository.CreateDeliveryAsync(delivery);
            return new IdempotencyDemoResponse<Delivery> { Success = true, Data = delivery };
        }

        public async Task<IdempotencyDemoResponse<Shipment>> UpdateDeliveryStatusAsync(string barcode, int statusId)
        {
            var stopwatch = Stopwatch.StartNew();
            var updatedShipment = await _deliveryRepository.UpdateDeliveryStatusAsync(barcode, statusId);
            stopwatch.Stop();
            if (updatedShipment == null)
            {
                return new IdempotencyDemoResponse<Shipment> { Success = false, Message = $"Shipment with barcode {barcode} not found." };
            }
            return new IdempotencyDemoResponse<Shipment>
            {
                Success = true,
                Data = updatedShipment,
                Message = "סטטוס משלוח עודכן בהצלחה",
                ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds
            };
        }
    }
}
