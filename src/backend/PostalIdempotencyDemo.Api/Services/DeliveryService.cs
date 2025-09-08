using PostalIdempotencyDemo.Api.Models;
using PostalIdempotencyDemo.Api.Repositories;
using System.Diagnostics;
using PostalIdempotencyDemo.Api.Services.Interfaces;

namespace PostalIdempotencyDemo.Api.Services
{

    public class DeliveryService : IDeliveryService
    {
        private readonly IDeliveryRepository _deliveryRepository;
        private readonly IMetricsRepository _metricsRepository;
        private readonly ILogger<DeliveryService> _logger;

        public DeliveryService(IDeliveryRepository deliveryRepository, IMetricsRepository metricsRepository, ILogger<DeliveryService> logger)
        {
            _deliveryRepository = deliveryRepository;
            _metricsRepository = metricsRepository;
            _logger = logger;
        }

        public async Task<(Shipment? Shipment, Delivery? Delivery)> GetShipmentAndDeliveryByBarcodeAsync(string barcode)
        {
            var Shipment = await _deliveryRepository.GetShipmentByBarcodeAsync(barcode);
            var Delivery = await _deliveryRepository.GetDeliveryByBarcodeAsync(barcode);
            return (Shipment, Delivery);
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
            await _metricsRepository.LogMetricsAsync("create_delivery", $"/api/idempotency-demo/delivery", 0, false, null);
            return new IdempotencyDemoResponse<Delivery> { Success = true, Data = delivery };
        }

        public async Task<IdempotencyDemoResponse<Shipment>> UpdateDeliveryStatusAsync(string barcode, int statusId, string requestPath)
        {
            var stopwatch = Stopwatch.StartNew();
            var updatedShipment = await _deliveryRepository.UpdateDeliveryStatusAsync(barcode, statusId);
            stopwatch.Stop();
            var executionTime = (int)stopwatch.ElapsedMilliseconds;
            if (updatedShipment == null)
            {
                await _metricsRepository.LogMetricsAsync("update_status", $"{requestPath}", executionTime, false, null);
                return new IdempotencyDemoResponse<Shipment> { Success = false, Message = $"Shipment with barcode {barcode} not found." };
            }
            await _metricsRepository.LogMetricsAsync("update_status", $"{requestPath}", executionTime, false, null);
            return new IdempotencyDemoResponse<Shipment>
            {
                Success = true,
                Data = updatedShipment,
                Message = "סטטוס משלוח עודכן בהצלחה",
                ExecutionTimeMs = executionTime
            };
        }

        public async Task LogIdempotentHitAsync(string barcode, string idempotencyKey, string requestPath)
        {
            _logger.LogDebug("רושם hit אידמפוטנטי עבור ברקוד {Barcode} עם מפתח {IdempotencyKey}", barcode, idempotencyKey);
            await _metricsRepository.LogMetricsAsync(
                operationType: "update_delivery_status", // סוג הפעולה
                endpoint: requestPath, // הנתיב שנקרא
                executionTimeMs: 0, // זמן ביצוע 0 כי זו בקשה שנחסמה
                isIdempotentHit: true, // זה hit אידמפוטנטי
                idempotencyKey: idempotencyKey // המפתח שגרם לחסימה               
            );
             _logger.LogInformation("Hit אידמפוטנטי נרשם בהצלחה עבור ברקוד {Barcode}", barcode);
        }
    }
}
