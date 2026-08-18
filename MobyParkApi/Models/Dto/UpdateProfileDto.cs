using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Models.Dto
{
	public class UpdateProfileDto
	{
		[MinLength(2)]
		public string? Name { get; set; }

		[EmailAddress]
		public string? Email { get; set; }

		public string? PhoneNumber { get; set; }

		[Range(1900, 2100)]
		public int? BirthYear { get; set; }

		[MinLength(8)]
		public string? Password { get; set; }
	}
}


