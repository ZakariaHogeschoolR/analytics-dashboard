using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models;

[Table("archived_users")]
public class ArchivedUsers
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int Id { get; set; }
    
    [Required]
    [Column("name")]
    public string Name { get; set; } = string.Empty;
    
    [Required]
    [Column("username")]
    public string Username { get; set; } = string.Empty;
    
    [Required]
    [Column("password")]
    public string Password { get; set; } = string.Empty;
    
    [Required]
    [Column("email")]
    public string Email { get; set; } = string.Empty;
    
    [Required]
    [Column("phone_number")]
    public string PhoneNumber { get; set; } = string.Empty;
    
    [Column("role")]
    public string Role { get; set; } = "User";
    
    [Column("created_at")]
    public DateTime? CreatedAt { get; set; }
    
    [Column("modified_at")]
    public DateTime? ModifiedAt { get; set; }
    
    [Column("birth_year")]
    public int? BirthYear { get; set; }
    
    [Column("active")]
    public bool? Active { get; set; }
    
    [Required]
    [Column("archived_at")]
    public DateTime ArchivedAt { get; set; } = DateTime.UtcNow;
    
    [Required]
    [Column("archived_by")]
    public string ArchivedBy { get; set; } = string.Empty;
}

