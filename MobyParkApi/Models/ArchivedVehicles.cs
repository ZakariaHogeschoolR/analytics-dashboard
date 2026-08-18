using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("archived_vehicles")]
public class ArchivedVehicles
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Column("user_id")]
    public int UserId { get; set; }
    
    [Required]
    [Column("license_plate")]
    public string LicensePlate { get; set; } = string.Empty;
    
    [Required]
    [Column("make")]
    public string Make { get; set; } = string.Empty;
    
    [Required]
    [Column("model")]
    public string Model { get; set; } = string.Empty;
    
    [Required]
    [Column("color")]
    public string Color { get; set; } = string.Empty;
    
    [Column("year")]
    public int? Year { get; set; }
    
    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime CreatedAt { get; set; }
    
    [Column("modified_at", TypeName = "timestamp without time zone")]
    public DateTime ModifiedAt { get; set; }
    
    [Required]
    [Column("archived_at", TypeName = "timestamp without time zone")]
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    [Column("archived_by")]
    public string ArchivedBy { get; set; } = string.Empty;
}

