using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("archived_parking_lots")]
public class ArchivedParkingLots
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [Column("location")]
    public string Location { get; set; } = string.Empty;
    
    [Required]
    [Column("address")]
    public string Address { get; set; } = string.Empty;
    
    [Column("capacity")]
    public int Capacity { get; set; }
    
    [Column("reserved")]
    public int Reserved { get; set; }
    
    [Column("tariff")]
    public double Tariff { get; set; }
    
    [Column("day_tariff")]
    public string DayTariff { get; set; } = string.Empty;
    
    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }
    
    [Column("modified_at", TypeName = "timestamp without time zone")]
    public DateTime? ModifiedAt { get; set; }
    
    [Required]
    [Column("coordinates")]
    public string Coordinates { get; set; } = string.Empty;
    
    [Required]
    [Column("archived_at", TypeName = "timestamp without time zone")]
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    [Column("archived_by")]
    public string ArchivedBy { get; set; } = string.Empty;
}

