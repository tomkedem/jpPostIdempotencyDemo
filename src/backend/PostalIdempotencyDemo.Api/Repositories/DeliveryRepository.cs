using System.Threading.Tasks;
using PostalIdempotencyDemo.Api.Repositories;

namespace PostalIdempotencyDemo.Api.Repositories
{
    public class DeliveryRepository : IDeliveryRepository
    {
        private readonly IConfiguration _configuration;

        public DeliveryRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task CreateDeliveryAsync(object delivery)
        {
            // TODO: Implement actual data access logic here
            await Task.CompletedTask;
        }

        public async Task<Models.Shipment?> UpdateDeliveryStatusAsync(string barcode, int statusId)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync();

            const string sql = "UPDATE deliveries SET status_id = @statusId WHERE barcode = @barcode";
            using var command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@statusId", statusId);
            command.Parameters.AddWithValue("@barcode", barcode);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            if (rowsAffected == 0)
            {
                return null;
            }

            return await GetShipmentByBarcodeAsync(barcode);
        }

        public async Task<Models.Shipment?> GetShipmentByBarcodeAsync(string barcode)
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");
            using var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await connection.OpenAsync();

            const string query = @"
                SELECT 
                    s.id, s.barcode, s.customer_name, s.address, s.weight, s.price, s.notes, s.created_at, s.updated_at,
                    d.status_id, st.status_name, st.status_name_he
                FROM shipments s
                LEFT JOIN deliveries d ON s.barcode = d.barcode
                LEFT JOIN shipment_statuses st ON d.status_id = st.id
                WHERE s.barcode = @barcode
                ORDER BY d.delivery_date DESC";

            using var command = new Microsoft.Data.SqlClient.SqlCommand(query, connection);
            command.Parameters.AddWithValue("@barcode", barcode);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Models.Shipment
                {
                    Id = reader.GetGuid(reader.GetOrdinal("id")),
                    Barcode = reader.GetString(reader.GetOrdinal("barcode")),
                    CustomerName = reader.IsDBNull(reader.GetOrdinal("customer_name")) ? null : reader.GetString(reader.GetOrdinal("customer_name")),
                    Address = reader.IsDBNull(reader.GetOrdinal("address")) ? null : reader.GetString(reader.GetOrdinal("address")),
                    Weight = reader.IsDBNull(reader.GetOrdinal("weight")) ? null : reader.GetDecimal(reader.GetOrdinal("weight")),
                    Price = reader.IsDBNull(reader.GetOrdinal("price")) ? null : reader.GetDecimal(reader.GetOrdinal("price")),
                    Notes = reader.IsDBNull(reader.GetOrdinal("notes")) ? null : reader.GetString(reader.GetOrdinal("notes")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                    UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime(reader.GetOrdinal("updated_at")),
                    StatusId = reader.IsDBNull(reader.GetOrdinal("status_id")) ? null : (int?)reader.GetInt32(reader.GetOrdinal("status_id")),
                    StatusName = reader.IsDBNull(reader.GetOrdinal("status_name")) ? null : reader.GetString(reader.GetOrdinal("status_name")),
                    StatusNameHe = reader.IsDBNull(reader.GetOrdinal("status_name_he")) ? null : reader.GetString(reader.GetOrdinal("status_name_he"))
                };
            }
            return null;
        }
    }
}
