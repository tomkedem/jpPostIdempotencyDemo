using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PostalIdempotencyDemo.Api.DTOs;

namespace PostalIdempotencyDemo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DeliveriesController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DeliveriesController> _logger;

        public DeliveriesController(IConfiguration configuration, ILogger<DeliveriesController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        [HttpPatch("{barcode}/status")]
        public async Task<IActionResult> UpdateDeliveryStatus(
            string barcode,
            [FromBody] UpdateStatusRequest request)
        {
            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                return BadRequest(new { error = "Missing Idempotency-Key header" });
            }

            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            // Check if delivery exists
            const string checkSql = "SELECT COUNT(1) FROM deliveries WHERE barcode = @barcode";
            using var checkCommand = new SqlCommand(checkSql, connection);
            checkCommand.Parameters.AddWithValue("@barcode", barcode);
            var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
            if (!exists)
            {
                return NotFound(new { error = $"Delivery with barcode {barcode} not found" });
            }

            // Optionally: Check idempotency table for this key and barcode
            // ...

            // Update status
            const string updateSql = "UPDATE deliveries SET status_id = @statusId, updated_at = @updatedAt WHERE barcode = @barcode";
            using var updateCommand = new SqlCommand(updateSql, connection);
            updateCommand.Parameters.AddWithValue("@statusId", (int)request.Status);
            updateCommand.Parameters.AddWithValue("@updatedAt", DateTime.UtcNow);
            updateCommand.Parameters.AddWithValue("@barcode", barcode);
            var rows = await updateCommand.ExecuteNonQueryAsync();
            if (rows == 0)
            {
                return StatusCode(500, new { error = "Failed to update delivery status" });
            }

            // Optionally: Save idempotency key usage
            // ...

            return Ok(new { barcode, status = request.Status.ToString(), statusId = (int)request.Status });
        }
    }
}
