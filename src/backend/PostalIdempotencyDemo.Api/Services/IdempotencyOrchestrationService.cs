using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PostalIdempotencyDemo.Api.Models;
using PostalIdempotencyDemo.Api.Repositories;
using PostalIdempotencyDemo.Api.Services.Interfaces;

namespace PostalIdempotencyDemo.Api.Services
{
    /// <summary>
    /// שירות ארגון הגנה אידמפוטנטית - מתאם בין שירותי האידמפוטנטיות והעסקיים
    /// </summary>
    public class IdempotencyOrchestrationService : IIdempotencyOrchestrationService
    {
        private readonly IIdempotencyService _idempotencyService;
        private readonly IDeliveryService _deliveryService;
        private readonly ISettingsRepository _settingsRepository;
        private readonly ILogger<IdempotencyOrchestrationService> _logger;

        public IdempotencyOrchestrationService(
            IIdempotencyService idempotencyService,
            IDeliveryService deliveryService,
            ISettingsRepository settingsRepository,
            ILogger<IdempotencyOrchestrationService> logger)
        {
            _idempotencyService = idempotencyService;
            _deliveryService = deliveryService;
            _settingsRepository = settingsRepository;
            _logger = logger;
        }

        /// <summary>
        /// עיבוד יצירת משלוח עם הגנה אידמפוטנטית מלאה
        /// </summary>
        public async Task<IdempotencyDemoResponse<Delivery>> ProcessCreateDeliveryWithIdempotencyAsync(
            CreateDeliveryRequest request,
            string idempotencyKey,
            string correlationId,
            string requestPath)
        {
            _logger.LogInformation("מעבד יצירת משלוח עם הגנה אידמפוטנטית. מפתח: {IdempotencyKey}", idempotencyKey);

            try
            {
                // שלב 1: בדיקה אם קיימת רשומה אידמפוטנטית קודמת
                _logger.LogDebug("מחפש רשומה אידמפוטנטית קיימת עבור correlation_id: {CorrelationId}", correlationId);
                var latestEntry = await _idempotencyService.GetLatestEntryByCorrelationIdAsync(correlationId);

                if (latestEntry != null && latestEntry.IdempotencyKey == idempotencyKey && latestEntry.ExpiresAt > DateTime.UtcNow)
                {
                    // נמצאה רשומה תקפה עם אותו מפתח - זו בקשה כפולה
                    _logger.LogInformation("נמצאה בקשה כפולה - מחזיר תשובה שמורה. מפתח: {IdempotencyKey}", idempotencyKey);

                    if (latestEntry.ResponseData != null)
                    {
                        var cachedResponse = JsonSerializer.Deserialize<IdempotencyDemoResponse<Delivery>>(latestEntry.ResponseData);
                        return cachedResponse ?? new IdempotencyDemoResponse<Delivery> { Success = false, Message = "שגיאה בקריאת תשובה שמורה" };
                    }
                }

                // שלב 2: יצירת רשומה אידמפוטנטית חדשה
                _logger.LogDebug("יוצר רשומה אידמפוטנטית חדשה");
                var expirationHours = await GetIdempotencyExpirationHoursAsync();

                var newEntry = new IdempotencyEntry
                {
                    Id = Guid.NewGuid().ToString(),
                    IdempotencyKey = idempotencyKey,
                    RequestHash = ComputeSha256Hash(JsonSerializer.Serialize(request)),
                    Endpoint = requestPath,
                    HttpMethod = "POST",
                    StatusCode = 0,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(expirationHours),
                    Operation = "create_delivery",
                    CorrelationId = correlationId,
                    RelatedEntityId = null
                };
                await _idempotencyService.StoreIdempotencyEntryAsync(newEntry);

                // שלב 3: עיבוד הבקשה בפועל
                _logger.LogInformation("מעבד בקשה חדשה ליצירת משלוח");
                var response = await _deliveryService.CreateDeliveryAsync(request);

                // שלב 4: שמירת התשובה למקרה של בקשות כפולות עתידיות
                await _idempotencyService.CacheResponseAsync(idempotencyKey, response);

                _logger.LogInformation("משלוח נוצר בהצלחה עם מפתח אידמפוטנטיות: {IdempotencyKey}", idempotencyKey);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה ביצירת משלוח עם מפתח אידמפוטנטיות: {IdempotencyKey}", idempotencyKey);
                return new IdempotencyDemoResponse<Delivery> { Success = false, Message = "שגיאה פנימית בשרת" };
            }
        }

        /// <summary>
        /// עיבוד עדכון סטטוס משלוח עם הגנה אידמפוטנטית מלאה
        /// </summary>
        public async Task<IdempotencyDemoResponse<Shipment>> ProcessUpdateDeliveryStatusWithIdempotencyAsync(
            string barcode,
            UpdateDeliveryStatusRequest request,
            string idempotencyKey,
            string requestPath)
        {
            _logger.LogInformation("מעבד עדכון סטטוס משלוח {Barcode} עם הגנה אידמפוטנטית. מפתח: {IdempotencyKey}", barcode, idempotencyKey);


            // שימוש ב-correlation_id לוגי ספציפי לברקוד זה
            var correlationId = requestPath;
            _logger.LogDebug("מחפש רשומה אידמפוטנטית קיימת עבור correlation_id: {CorrelationId}", correlationId);

            var latestEntry = await _idempotencyService.GetLatestEntryByCorrelationIdAsync(correlationId);

            if (latestEntry != null && latestEntry.IdempotencyKey == idempotencyKey && latestEntry.ExpiresAt > DateTime.UtcNow)
            {
                // זו בקשה כפולה - חוסמים ומתעדים בלוג
                _logger.LogWarning("בקשה כפולה לעדכון סטטוס זוהתה וחסומה. ברקוד: {Barcode}, מפתח: {IdempotencyKey}", barcode, idempotencyKey);

                if (latestEntry.ResponseData != null)
                {
                    // תיעוד hit אידמפוטנטי בטבלת operation_metrics
                    await _deliveryService.LogIdempotentHitAsync(barcode, idempotencyKey, requestPath);

                    // החזרת הודעה עקבית על חסימה
                    return new IdempotencyDemoResponse<Shipment>
                    {
                        Success = true,
                        Data = null,
                        Message = "העדכון נחסם בגלל מפתח אידמפונטנטי, סטטוס לא שונה."
                    };
                }
            }

            // יצירת רשומה אידמפוטנטית חדשה
            _logger.LogDebug("יוצר רשומה אידמפוטנטית חדשה לעדכון סטטוס");
            var expirationHours = await GetIdempotencyExpirationHoursAsync();

            var newEntry = new IdempotencyEntry
            {
                Id = Guid.NewGuid().ToString(),
                IdempotencyKey = idempotencyKey,
                RequestHash = ComputeSha256Hash(JsonSerializer.Serialize(request)),
                Endpoint = requestPath,
                HttpMethod = "PATCH",
                StatusCode = 0,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(expirationHours),
                Operation = "update_delivery_status",
                CorrelationId = correlationId,
                RelatedEntityId = barcode
            };
            await _idempotencyService.StoreIdempotencyEntryAsync(newEntry);

            // עדכון הסטטוס בפועל
            _logger.LogInformation("מעדכן סטטוס משלוח {Barcode} לסטטוס {StatusId}", barcode, request.StatusId);
            var response = await _deliveryService.UpdateDeliveryStatusAsync(barcode, request.StatusId);

            // שמירת התשובה למקרה של בקשות כפולות עתידיות
            await _idempotencyService.CacheResponseAsync(idempotencyKey, response);

            if (!response.Success)
            {
                _logger.LogWarning("עדכון סטטוס נכשל עבור ברקוד {Barcode}: {Message}", barcode, response.Message);
            }
            else
            {
                _logger.LogInformation("סטטוס משלוח {Barcode} עודכן בהצלחה", barcode);
            }

            return response;
        }

        /// <summary>
        /// קריאת זמן תפוגה אידמפוטנטיות מטבלת SystemSettings
        /// </summary>
        private async Task<int> GetIdempotencyExpirationHoursAsync()
        {
            _logger.LogDebug("קורא זמן תפוגה אידמפוטנטיות מטבלת ההגדרות");

            var settings = await _settingsRepository.GetSettingsAsync();
            var expirationSetting = settings.FirstOrDefault(s => s.SettingKey == "IdempotencyExpirationHours");

            if (expirationSetting != null && int.TryParse(expirationSetting.SettingValue, out int hours) && hours > 0)
            {
                _logger.LogDebug("זמן תפוגה אידמפוטנטיות נקרא מההגדרות: {Hours} שעות", hours);
                return hours;
            }

            _logger.LogWarning("לא נמצא זמן תפוגה תקין בהגדרות, משתמש בברירת מחדל: 24 שעות");
            return 24;
        }

        /// <summary>
        /// חישוב hash SHA256 עבור תוכן הבקשה
        /// </summary>
        private string ComputeSha256Hash(string input)
        {
            _logger.LogDebug("מחשב SHA256 hash עבור תוכן בקשה");
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
            var result = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            _logger.LogDebug("SHA256 hash חושב: {HashPrefix}...", result[..8]);
            return result;
        }
    }
}
