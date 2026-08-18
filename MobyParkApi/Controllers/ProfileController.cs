using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MobyParkApi.Models.Dto;
using MobyParkApi.Services;
using System.Security.Claims;

namespace MobyParkApi.Controllers
{
    [SwaggerOrder(2)]
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProfileController : ControllerBase
    {
        private readonly IProfileService _profileService;

        public ProfileController(IProfileService profileService)
        {
            _profileService = profileService;
        }

        /// <summary>
        /// GET /api/Profile - Haal profiel op van ingelogde gebruiker
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProfile()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("Geen geldige gebruiker gevonden");

            var user = await _profileService.GetProfileAsync(userId);
            if (user == null)
                return NotFound("Gebruiker niet gevonden");

            return Ok(new
            {
                id = user.Id,
                name = user.Name,
                username = user.Username,
                email = user.Email,
                phoneNumber = user.Phone_Number,
                birthYear = user.Birth_Year,
                role = user.Role,
                active = user.Active,
                createdAt = user.Created_At,
                modifiedAt = user.Modified_At
            });
        }

        /// <summary>
        /// PUT /api/Profile - Update profiel van ingelogde gebruiker
        /// </summary>
        [HttpPut]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("Geen geldige gebruiker gevonden");

            var user = await _profileService.UpdateProfileAsync(userId, request);
            if (user == null)
                return NotFound("Gebruiker niet gevonden");

            return Ok("Profiel succesvol bijgewerkt ✅");
        }

        /// <summary>
        /// POST /api/Profile - Create or update profiel van ingelogde gebruiker
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateOrUpdateProfile([FromBody] UpdateProfileDto request)
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("Geen geldige gebruiker gevonden");

            var user = await _profileService.UpdateProfileAsync(userId, request);
            if (user == null)
                return NotFound("Gebruiker niet gevonden");

            return Ok("Profiel succesvol bijgewerkt ✅");
        }

        /// <summary>
        /// DELETE /api/Profile - Deactiveer profiel van ingelogde gebruiker (soft delete)
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> DeleteProfile()
        {
            if (!TryGetUserId(out var userId))
                return Unauthorized("Geen geldige gebruiker gevonden");

            var success = await _profileService.DeactivateProfileAsync(userId);
            if (!success)
                return NotFound("Gebruiker niet gevonden");

            return Ok("Profiel succesvol gedeactiveerd ✅");
        }

        /// <summary>
        /// POST /api/Profile/reactivate - Reactiveer gedeactiveerd profiel (binnen 30 dagen)
        /// </summary>
        [AllowAnonymous]
        [HttpPost("reactivate")]
        public async Task<IActionResult> ReactivateProfile([FromBody] ReactivateProfileDto request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await _profileService.ReactivateProfileAsync(request);
            
            if (!result.Success)
                return BadRequest(result.ErrorMessage);

            return Ok("Profiel succesvol gereactiveerd ✅");
        }

        private bool TryGetUserId(out int userId)
        {
            userId = 0;
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return !string.IsNullOrEmpty(userIdClaim) && int.TryParse(userIdClaim, out userId);
        }
    }
}