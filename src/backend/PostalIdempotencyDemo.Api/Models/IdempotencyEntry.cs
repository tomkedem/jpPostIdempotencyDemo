using System.ComponentModel.DataAnnotations;

namespace PostalIdempotencyDemo.Api.Models;

public class IdempotencyEntry
{
    public string? RequestHash { get; set; }
    public string Id { get; set; } = string.Empty;
    
    [Required]
    [StringLength(64)]
    public string IdempotencyKey { get; set; } = string.Empty;
    
    [Required]
    [StringLength(64)]
    public string RequestSignature { get; set; } = string.Empty;
    
    public string? ResponseData { get; set; }
    
    [Required]
    public int StatusCode { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    
    [Required]
    [StringLength(100)]
    public string Endpoint { get; set; } = string.Empty;
    
    [Required]
    [StringLength(10)]
    public string HttpMethod { get; set; } = string.Empty;
}
