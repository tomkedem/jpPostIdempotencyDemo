using Microsoft.AspNetCore.Mvc;
using PostalIdempotencyDemo.Api.Models;
using System.Security.Cryptography;
using System.Text;
using PostalIdempotencyDemo.Api.Repositories;
using PostalIdempotencyDemo.Api.Services.Interfaces;
using System.Text.Json;

namespace PostalIdempotencyDemo.Api.Controllers;

/// <summary>
/// קונטרולר לניהול פעולות אידמפוטנטיות במערכת משלוחים
/// מספק הגנה מפני פעולות כפולות באמצעות Idempotency Keys
/// </summary>
[ApiController]
[Route("api/idempotency-demo")]
public class IdempotencyDemoController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdempotencyDemoController> _logger;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IDeliveryService _deliveryService;
    private readonly ISettingsRepository _settingsRepository;
    private static readonly Random _random = new();

    /// <summary>
    /// קונסטרקטור - מאתחל את כל השירותים הנדרשים
    /// </summary>
    public IdempotencyDemoController(
        IConfiguration configuration,
        ILogger<IdempotencyDemoController> logger,
        IIdempotencyService idempotencyService,
        IDeliveryService deliveryService,
        ISettingsRepository settingsRepository)
    {
        _configuration = configuration;
        _logger = logger;
        _idempotencyService = idempotencyService;
        _deliveryService = deliveryService;
        _settingsRepository = settingsRepository;
    }

    /// <summary>
    /// יצירת משלוח חדש עם הגנת אידמפוטנטיות
    /// </summary>
    /// <param name="request">פרטי המשלוח החדש</param>
    /// <param name="idempotencyKey">מפתח אידמפוטנטיות ייחודי (נדרש בכותרת)</param>
    /// <returns>פרטי המשלוח שנוצר או התשובה השמורה אם זו בקשה כפולה</returns>
    /// <remarks>
    /// אלגוריתם ההגנה:
    /// 1. בדיקה אם קיים מפתח אידמפוטנטיות בכותרת
    /// 2. חיפוש רשומה קיימת לפי correlation_id
    /// 3. אם נמצאה רשומה עם אותו מפתח ולא פגה - החזרת התשובה השמורה
    /// 4. אחרת - יצירת רשומה חדשה, עיבוד הבקשה ושמירת התשובה
    /// </remarks>
    [HttpPost("delivery")]
    public async Task<IActionResult> CreateDelivery([FromBody] CreateDeliveryRequest request, [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
    {
        _logger.LogInformation("התקבלה בקשה ליצירת משלוח חדש עם מפתח אידמפוטנטיות: {IdempotencyKey}", idempotencyKey);

        // ולידציה - מפתח אידמפוטנטיות הוא חובה
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            _logger.LogWarning("בקשה נדחתה - חסר מפתח אידמפוטנטיות");
            return BadRequest(new IdempotencyDemoResponse<object> { Success = false, Message = "Idempotency-Key header is required." });
        }

        try
        {
            // שלב 1: בדיקה אם קיימת רשומה אידמפוטנטית קודמת
            _logger.LogDebug("מחפש רשומה אידמפוטנטית קיימת עבור correlation_id: {CorrelationId}", HttpContext.TraceIdentifier);
            IdempotencyEntry? latestEntry = await _idempotencyService.GetLatestEntryByCorrelationIdAsync(HttpContext.TraceIdentifier);

            if (latestEntry != null && latestEntry.IdempotencyKey == idempotencyKey && latestEntry.ExpiresAt > DateTime.UtcNow)
            {
                // נמצאה רשומה תקפה עם אותו מפתח - זו בקשה כפולה
                _logger.LogInformation("נמצאה בקשה כפולה - מחזיר תשובה שמורה. מפתח: {IdempotencyKey}", idempotencyKey);

                if (latestEntry.ResponseData != null)
                {
                    return Ok(JsonSerializer.Deserialize<object>(latestEntry.ResponseData));
                }
            }

            // שלב 2: יצירת רשומה אידמפוטנטית חדשה
            _logger.LogDebug("יוצר רשומה אידמפוטנטית חדשה");

            // קריאת זמן תפוגה מההגדרות במקום ערך קשיח
            var expirationHours = await GetIdempotencyExpirationHoursAsync();

            var newEntry = new IdempotencyEntry
            {
                IdempotencyKey = idempotencyKey,
                RequestHash = ComputeSha256Hash(JsonSerializer.Serialize(request)),
                Endpoint = HttpContext.Request.Path,
                HttpMethod = HttpContext.Request.Method,
                StatusCode = 0,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(expirationHours), // שימוש בערך מההגדרות
                Operation = "create_delivery",
                CorrelationId = HttpContext.TraceIdentifier,
                RelatedEntityId = null
            };
            await _idempotencyService.StoreIdempotencyEntryAsync(newEntry);

            // שלב 3: עיבוד הבקשה בפועל
            _logger.LogInformation("מעבד בקשה חדשה ליצירת משלוח");
            IdempotencyDemoResponse<Delivery> response = await _deliveryService.CreateDeliveryAsync(request);

            // שלב 4: שמירת התשובה למקרה של בקשות כפולות עתידיות
            await _idempotencyService.CacheResponseAsync(idempotencyKey, response);

            _logger.LogInformation("משלוח נוצר בהצלחה עם מפתח אידמפוטנטיות: {IdempotencyKey}", idempotencyKey);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "שגיאה ביצירת משלוח עם מפתח אידמפוטנטיות: {IdempotencyKey}", idempotencyKey);
            return StatusCode(500, new IdempotencyDemoResponse<object> { Success = false, Message = "שגיאה פנימית בשרת" });
        }
    }

    /// <summary>
    /// עדכון סטטוס משלוח עם הגנת אידמפוטנטיות
    /// </summary>
    /// <param name="barcode">ברקוד המשלוח</param>
    /// <param name="request">הסטטוס החדש</param>
    /// <param name="idempotencyKey">מפתח אידמפוטנטיות ייחודי</param>
    /// <returns>תוצאת העדכון או הודעה על חסימה</returns>
    /// <remarks>
    /// אלגוריתם מיוחד לעדכון סטטוס:
    /// 1. משתמש ב-correlation_id לוגי (נתיב + ברקוד) במקום TraceIdentifier
    /// 2. אם זו בקשה כפולה - חוסם ומחזיר הודעה מתאימה
    /// 3. אם זו בקשה חדשה - מעדכן את הסטטוס ושומר את התשובה
    /// </remarks>
    [HttpPatch("delivery/{barcode}/status")]
    public async Task<IActionResult> UpdateDeliveryStatus(
        [FromRoute] string barcode,
        [FromBody] UpdateDeliveryStatusRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey)
    {
        _logger.LogInformation("התקבלה בקשה לעדכון סטטוס משלוח {Barcode} עם מפתח אידמפוטנטיות: {IdempotencyKey}", barcode, idempotencyKey);

        // ולידציה - מפתח אידמפוטנטיות הוא חובה
        if (string.IsNullOrEmpty(idempotencyKey))
        {
            _logger.LogWarning("בקשה נדחתה - חסר מפתח אידמפוטנטיות");
            return BadRequest(new IdempotencyDemoResponse<object> { Success = false, Message = "Idempotency-Key header is required." });
        }
        try
        {
            // שימוש ב-correlation_id לוגי ספציפי לברקוד זה
            string correlationId = $"/api/idempotency-demo/delivery/{barcode}/status";
            _logger.LogDebug("מחפש רשומה אידמפוטנטית קיימת עבור correlation_id: {CorrelationId}", correlationId);

            IdempotencyEntry? latestEntry = await _idempotencyService.GetLatestEntryByCorrelationIdAsync(correlationId);

            if (latestEntry != null && latestEntry.IdempotencyKey == idempotencyKey && latestEntry.ExpiresAt > DateTime.UtcNow)
            {
                // זו בקשה כפולה - חוסמים ומתעדים בלוג
                _logger.LogWarning("בקשה כפולה לעדכון סטטוס זוהתה וחסומה. ברקוד: {Barcode}, מפתח: {IdempotencyKey}", barcode, idempotencyKey);

                if (latestEntry.ResponseData != null)
                {
                    // תיעוד hit אידמפוטנטי בטבלת operation_metrics
                    // רושם שבקשה כפולה זוהתה ונחסמה בהצלחה על ידי מערכת ההגנה
                    // הנתונים ישמשו לחישוב מדדי יעילות ההגנה האידמפוטנטית
                    await _deliveryService.LogIdempotentHitAsync(barcode, idempotencyKey, HttpContext.Request.Path);

                    // החזרת הודעה עקבית על חסימה
                    return Ok(new IdempotencyDemoResponse<Shipment>
                    {
                        Success = true,
                        Data = null,
                        Message = "העדכון נחסם בגלל מפתח אידמפונטנטי, סטטוס לא שונה."
                    });
                }
            }

            var expirationHours = await GetIdempotencyExpirationHoursAsync();
            // יצירת רשומה אידמפוטנטית חדשה
            _logger.LogDebug("יוצר רשומה אידמפוטנטית חדשה לעדכון סטטוס");
            var newEntry = new IdempotencyEntry
            {
                IdempotencyKey = idempotencyKey,
                RequestHash = ComputeSha256Hash(JsonSerializer.Serialize(request)),
                Endpoint = HttpContext.Request.Path,
                HttpMethod = HttpContext.Request.Method,
                StatusCode = 0,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(expirationHours), // שימוש בערך מההגדרות
                Operation = "update_delivery_status",
                CorrelationId = correlationId,
                RelatedEntityId = barcode // שמירת הברקוד למעקב
            };
            await _idempotencyService.StoreIdempotencyEntryAsync(newEntry);

            // עדכון הסטטוס בפועל
            _logger.LogInformation("מעדכן סטטוס משלוח {Barcode} לסטטוס {StatusId}", barcode, request.StatusId);
            IdempotencyDemoResponse<Shipment> response = await _deliveryService.UpdateDeliveryStatusAsync(barcode, request.StatusId);

            // שמירת התשובה למקרה של בקשות כפולות עתידיות
            await _idempotencyService.CacheResponseAsync(idempotencyKey, response);

            if (!response.Success)
            {
                _logger.LogWarning("עדכון סטטוס נכשל עבור ברקוד {Barcode}: {Message}", barcode, response.Message);
                return NotFound(response);
            }

            _logger.LogInformation("סטטוס משלוח {Barcode} עודכן בהצלחה", barcode);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "שגיאה בעדכון סטטוס משלוח {Barcode} עם מפתח אידמפוטנטיות: {IdempotencyKey}", barcode, idempotencyKey);
            return StatusCode(500, new IdempotencyDemoResponse<object> { Success = false, Message = "שגיאה פנימית בשרת" });
        }
    }

    /// <summary>
    /// סימולציה של בעיות רשת לצורכי בדיקה
    /// </summary>
    /// <remarks>
    /// מוסיף עיכובים אקראיים ושגיאות לצורך בדיקת התנהגות המערכת
    /// תחת תנאי רשת לא יציבים
    /// </remarks>
    private async Task SimulateNetworkIssues()
    {
        bool enableNetworkIssues = _configuration.GetValue<bool>("NetworkSimulation:EnableNetworkIssues");
        if (!enableNetworkIssues)
        {
            _logger.LogDebug("סימולצית בעיות רשת מבוטלת");
            return;
        }

        int minDelay = _configuration.GetValue<int>("NetworkSimulation:MinDelayMs", 100);
        int maxDelay = _configuration.GetValue<int>("NetworkSimulation:MaxDelayMs", 3000);
        int failurePercentage = _configuration.GetValue<int>("NetworkSimulation:FailurePercentage", 10);

        // עיכוב אקראי
        int delay = _random.Next(minDelay, maxDelay);
        _logger.LogDebug("מדמה עיכוב רשת של {Delay}ms", delay);
        await Task.Delay(delay);

        // כישלון אקראי
        if (_random.Next(1, 101) <= failurePercentage)
        {
            _logger.LogWarning("מדמה כישלון רשת (timeout)");
            throw new TimeoutException("Network timeout simulation");
        }
    }

    /// <summary>
    /// חישוב hash SHA256 עבור תוכן הבקשה
    /// </summary>
    /// <param name="input">המחרוזת לעבוד עליה hash</param>
    /// <returns>hash בפורמט הקסדצימלי</returns>
    /// <remarks>
    /// משמש ליצירת חתימה ייחודית לתוכן הבקשה
    /// מאפשר זיהוי שינויים בתוכן הבקשה עבור אותו מפתח אידמפוטנטיות
    /// </remarks>
    private string ComputeSha256Hash(string input)
    {
        _logger.LogDebug("מחשב SHA256 hash עבור תוכן בקשה");
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(input));
        string result = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        _logger.LogDebug("SHA256 hash חושב: {HashPrefix}...", result[..8]);
        return result;
    }

    /// <summary>
    /// קריאת זמן תפוגה אידמפוטנטיות מטבלת SystemSettings
    /// </summary>
    /// <returns>מספר שעות תפוגה, ברירת מחדל 24 שעות</returns>
    /// <remarks>
    /// קוראת את הערך IdempotencyExpirationHours מטבלת SystemSettings
    /// אם לא נמצא הערך או יש שגיאה - מחזירה 24 שעות כברירת מחדל
    /// מאפשרת הגדרה דינמית של זמני תפוגה ללא שינוי קוד
    /// </remarks>
    private async Task<int> GetIdempotencyExpirationHoursAsync()
    {
        try
        {
            _logger.LogDebug("קורא זמן תפוגה אידמפוטנטיות מטבלת ההגדרות");

            var settings = await _settingsRepository.GetSettingsAsync();
            var expirationSetting = settings.FirstOrDefault(s => s.SettingKey == "IdempotencyExpirationHours");

            if (expirationSetting != null && int.TryParse(expirationSetting.SettingValue, out int hours) && hours > 0)
            {
                _logger.LogDebug("זמן תפוגה אידמפוטנטיות נקרא מההגדרות: {Hours} שעות", hours);
                return hours;
            }

            // ברירת מחדל אם לא נמצא בהגדרות או הערך לא תקין
            _logger.LogWarning("לא נמצא זמן תפוגה תקין בהגדרות (נמצא: '{Value}'), משתמש בברירת מחדל: 24 שעות",
                expirationSetting?.SettingValue ?? "null");
            return 24;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "שגיאה בקריאת זמן תפוגה מההגדרות, משתמש בברירת מחדל: 24 שעות");
            return 24;
        }
    }
}