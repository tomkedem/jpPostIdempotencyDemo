using System.ComponentModel.DataAnnotations;

namespace PostalIdempotencyDemo.Api.DTOs;

public class CreateShipmentRequest
{
    [Required]
    [StringLength(50)]
    public string Barcode { get; set; } = string.Empty;
    
    [Required]
    public int KodPeula { get; set; }
    
    [Required]
    public int PerutPeula { get; set; }
    
    [Required]
    public int Atar { get; set; }
    
    [StringLength(100)]
    public string? CustomerName { get; set; }
    
    [StringLength(200)]
    public string? Address { get; set; }
    
    [Required]
    [Range(0.001, double.MaxValue)]
    public decimal Weight { get; set; }
    
    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Price { get; set; }
    
    [StringLength(500)]
    public string? Notes { get; set; }
}
