using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using PostalIdempotencyDemo.Api.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PostalIdempotencyDemo.Api.Services.Interfaces;

namespace PostalIdempotencyDemo.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SimpleShipmentsController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SimpleShipmentsController> _logger;
        private readonly IShipmentService _shipmentService;

        public SimpleShipmentsController(IConfiguration configuration, ILogger<SimpleShipmentsController> logger, IShipmentService shipmentService)
        {
            _configuration = configuration;
            _logger = logger;
            _shipmentService = shipmentService;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAllShipments()
        {
            var result = await _shipmentService.GetAllShipmentsAsync();
            if (!result.IsSuccess || result.Data == null)
            {
                return StatusCode(500, new { error = result.ErrorMessage ?? "Failed to retrieve shipments" });
            }
            var shipments = result.Data.Select(shipment => new
            {
                id = shipment.Id,
                barcode = shipment.Barcode,
                kodPeula = shipment.KodPeula,
                perutPeula = shipment.PerutPeula,
                atar = shipment.Atar,
                customerName = shipment.CustomerName,
                address = shipment.Address,
                weight = shipment.Weight,
                price = shipment.Price,
                statusId = shipment.StatusId,
                statusNameHe = shipment.StatusNameHe ?? "לא עודכן",
                createdAt = shipment.CreatedAt,
                updatedAt = shipment.UpdatedAt,
                notes = shipment.Notes
            });
            return Ok(shipments);
        }

        [HttpGet("{barcode}")]
        public async Task<ActionResult<object>> GetShipmentByBarcode(string barcode)
        {
            var result = await _shipmentService.GetShipmentByBarcodeAsync(barcode);
            if (!result.IsSuccess || result.Data == null)
            {
                return NotFound(new { error = $"Shipment with barcode {barcode} not found" });
            }
            var shipment = result.Data;
            // ניתן להחזיר אובייקט אנונימי עם השדות הנדרשים בלבד
            return Ok(new
            {
                id = shipment.Id,
                barcode = shipment.Barcode,
                kodPeula = shipment.KodPeula,
                perutPeula = shipment.PerutPeula,
                atar = shipment.Atar,
                customerName = shipment.CustomerName,
                address = shipment.Address,
                weight = shipment.Weight,
                price = shipment.Price,
                statusId = shipment.StatusId,
                statusNameHe = shipment.StatusNameHe ?? "לא עודכן",
                createdAt = shipment.CreatedAt,
                updatedAt = shipment.UpdatedAt,
                notes = shipment.Notes
            });
        }

        [HttpPost]
        public async Task<ActionResult<object>> CreateShipment([FromBody] CreateShipmentRequest request)
        {
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                _logger.LogInformation("SimpleShipments using connection string: {ConnectionString}", connectionString);

                // Debug: Show all configuration sources
                var allConnectionStrings = _configuration.GetSection("ConnectionStrings").GetChildren();
                foreach (var cs in allConnectionStrings)
                {
                    _logger.LogInformation("Config key: {Key}, Value: {Value}", cs.Key, cs.Value);
                }

                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                // Check if barcode exists
                const string checkSql = "SELECT COUNT(1) FROM shipments WHERE barcode = @barcode";
                using var checkCommand = new SqlCommand(checkSql, connection);
                checkCommand.Parameters.AddWithValue("@barcode", request.Barcode);

                var exists = Convert.ToInt32(await checkCommand.ExecuteScalarAsync()) > 0;
                if (exists)
                {
                    return Conflict(new { error = $"Shipment with barcode {request.Barcode} already exists" });
                }

                var newId = Guid.NewGuid();
                const string sql = @"
                INSERT INTO shipments (id, barcode, kod_peula, perut_peula, atar, customer_name, 
                                     address, weight, price, status_id, notes, created_at)
                VALUES (@id, @barcode, @kod_peula, @perut_peula, @atar, @customer_name, 
                        @address, @weight, @price, @status_id, @notes, @created_at);
                
                SELECT id, created_at FROM shipments WHERE id = @id";

                using var command = new SqlCommand(sql, connection);
                command.Parameters.AddWithValue("@id", newId);
                command.Parameters.AddWithValue("@barcode", request.Barcode);
                command.Parameters.AddWithValue("@kod_peula", request.KodPeula);
                command.Parameters.AddWithValue("@perut_peula", request.PerutPeula);
                command.Parameters.AddWithValue("@atar", request.Atar);
                command.Parameters.AddWithValue("@customer_name", request.CustomerName ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@address", request.Address ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@weight", request.Weight);
                command.Parameters.AddWithValue("@price", request.Price);
                command.Parameters.AddWithValue("@status_id", 1); // Created
                command.Parameters.AddWithValue("@notes", request.Notes ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("@created_at", DateTime.UtcNow);

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    var result = new
                    {
                        id = reader.GetGuid(0),
                        barcode = request.Barcode,
                        kodPeula = request.KodPeula,
                        perutPeula = request.PerutPeula,
                        atar = request.Atar,
                        customerName = request.CustomerName,
                        address = request.Address,
                        weight = request.Weight,
                        price = request.Price,
                        statusId = 1,
                        createdAt = reader.GetDateTime(1),
                        notes = request.Notes
                    };

                    _logger.LogInformation("Created shipment with barcode {Barcode}", request.Barcode);
                    return CreatedAtAction(nameof(GetShipmentByBarcode), new { barcode = request.Barcode }, result);
                }

                return StatusCode(500, new { error = "Failed to create shipment" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating shipment with barcode {Barcode}", request.Barcode);
                return StatusCode(500, new { error = "Failed to create shipment", details = ex.Message });
            }
        }
    }
}

public class CreateShipmentRequest
{
    public string Barcode { get; set; } = string.Empty;
    public int KodPeula { get; set; }
    public int PerutPeula { get; set; }
    public int Atar { get; set; }
    public string? CustomerName { get; set; }
    public string? Address { get; set; }
    public decimal Weight { get; set; }
    public decimal Price { get; set; }
    public string? Notes { get; set; }
}
