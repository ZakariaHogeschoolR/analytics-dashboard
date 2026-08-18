using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MobyParkApi.Models
{
    [Table("users")]
    public class Users
    {
        [Key]
        [Column("id")]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Column("name")]
        public string Name { get; set; } = "";

        [Required]
        [Column("username")]
        public string Username { get; set; } = "";

        [Required]
        [Column("password")]
        public string Password { get; set; } = "";

        [Required]
        [Column("email")]
        public string Email { get; set; } = "";

        [Required]
        [Column("phone_number")]
        public string Phone_Number { get; set; } = "";

        [Column("role")]
        public string Role { get; set; } = "User";

        [Column("created_at", TypeName = "timestamp without time zone")]
        public DateTime? Created_At { get; set; }

        [Column("modified_at", TypeName = "timestamp without time zone")]
        public DateTime? Modified_At { get; set; }

        [Column("birth_year")]
        public int? Birth_Year { get; set; }

        [Column("active")]
        public bool? Active { get; set; }
    }
}