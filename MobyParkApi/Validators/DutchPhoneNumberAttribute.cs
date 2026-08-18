using System.ComponentModel.DataAnnotations;

namespace MobyParkApi.Validators
{
    public class DutchPhoneNumberAttribute : ValidationAttribute
    {
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            // Null check
            if (value == null)
            {
                return new ValidationResult("Telefoonnummer is verplicht");
            }

            string? phoneNumberString = value.ToString();
            
            if (string.IsNullOrWhiteSpace(phoneNumberString))
            {
                return new ValidationResult("Telefoonnummer is verplicht");
            }

            // Verwijder formatting karakters
            string phoneNumber = phoneNumberString
                .Replace(" ", "")
                .Replace("-", "")
                .Replace("(", "")
                .Replace(")", "");

            // Check 1: Moet alleen cijfers bevatten
            if (!phoneNumber.All(char.IsDigit))
            {
                return new ValidationResult("Telefoonnummer mag alleen cijfers bevatten");
            }

            // Check 2: Moet beginnen met 06
            if (!phoneNumber.StartsWith("06"))
            {
                return new ValidationResult("Telefoonnummer moet beginnen met 06");
            }

            // Check 3: Moet exact 10 cijfers zijn
            if (phoneNumber.Length != 10)
            {
                return new ValidationResult($"Telefoonnummer moet exact 10 cijfers zijn (06 + 8 cijfers). Je hebt {phoneNumber.Length} cijfers ingevoerd.");
            }

            return ValidationResult.Success;
        }
    }
}