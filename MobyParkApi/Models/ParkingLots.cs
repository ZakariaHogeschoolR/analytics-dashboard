using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("parking_lots")]
public class ParkingLots
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Required]
    [Column("name")]
    public string Name { get; set; } = String.Empty;
    
    [Required]
    [Column("location")]
    public string Location { get; set; } = String.Empty;
    
    [Required]
    [Column("address")]
    public string Address { get; set; } = String.Empty;
    
    [Column("capacity")]
    public int Capacity { get; set; }
    
    [Column("reserved")]
    public int Reserved { get; set; }
    
    [Column("tariff")]
    public decimal Tariff { get; set; }
    
    [Column("day_tariff")]
    public decimal DayTariff { get; set; }
    
    [Column("created_at", TypeName = "timestamp without time zone")]
    public DateTime? CreatedAt { get; set; }
    
    [Column("modified_at", TypeName = "timestamp without time zone")]
    public DateTime? ModifiedAt { get; set; }
    
    [Required]
    [Column("coordinates")]
    public string Coordinates { get; set; } = String.Empty;
}