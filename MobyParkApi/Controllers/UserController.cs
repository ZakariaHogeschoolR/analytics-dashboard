using Microsoft.AspNetCore.Mvc;
using MobyParkApi.Services;
using MobyParkApi.Models.Dto;
using Microsoft.AspNetCore.Authorization;
using MobyParkApi.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace MobyParkApi.Controllers
{
    [SwaggerOrder(1)]
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IUsersService _usersService;
        private readonly IAuthService _authService;
        private readonly ApplicationDbContext _context; 

        public UsersController(IUsersService usersService, IAuthService authService, ApplicationDbContext context)
        {
            _usersService = usersService;
            _authService = authService;
            _context = context; 
        }

        /// <summary>
        /// POST /api/Users/register - Registreer een nieuwe gebruiker
        /// </summary>
        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterUserDto request)
        {
            if (!ModelState.IsValid){
                return BadRequest(ModelState);
            }

            var result = await _usersService.RegisterAsync(request);

            if (!result.Success)
                return BadRequest(result.ErrorMessage);

            return Ok("Account succesvol aangemaakt ✅");
        }

        /// <summary>
        /// POST /api/Users/login - Log in en ontvang JWT token
        /// </summary>
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginUserDto request)
        {   
            if (!ModelState.IsValid) 
            {
                return BadRequest(ModelState);
            }

            try 
            {
                var user = await _usersService.LoginAsync(request);

                if (user == null)
                    return Unauthorized("Ongeldige gebruikersnaam of wachtwoord");

                var token = _authService.GenerateToken(user);

                return Ok(new
                {
                    message = "Inloggen succesvol!",
                    accessToken = token,
                    role = user.Role
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(ex.Message);
            }
            catch (Exception ex) 
            {
                return StatusCode(500, "Er is een fout opgetreden bij het inloggen");
            }
        }

        /// <summary>
        /// POST /api/Users/logout - Log uit (client moet token lokaal verwijderen)
        /// </summary>
        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            return Ok(new { message = "Uitloggen succesvol" });
        }

        /// <summary>
        /// GET /api/Users/all - Haal alle gebruikers op met pagination (ADMIN only)
        /// </summary>
        [Authorize(Roles = "Admin")] 
        [HttpGet("all")]
        public async Task<IActionResult> GetAllUsers([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            try
            {
                if (page < 1)
                    page = 1;
                if (pageSize < 1 || pageSize > 100)
                    pageSize = 10;

                var totalCount = await _context.Users.CountAsync();
                var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

                var users = await _context.Users
                    .OrderBy(usr => usr.Username)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(usr => new
                    {
                        id = usr.Id,
                        name = usr.Name,
                        username = usr.Username,
                        email = usr.Email,
                        phoneNumber = usr.Phone_Number,
                        role = usr.Role,
                        active = usr.Active,
                        birthYear = usr.Birth_Year,
                        createdAt = usr.Created_At,
                        modifiedAt = usr.Modified_At
                    }).ToListAsync();

                return Ok(new 
                {
                    data = users,
                    pagination = new 
                    {
                        currentPage = page,
                        pageSize = pageSize, 
                        totalCount = totalCount,
                        totalPages = totalPages,
                        hasNextPage = page < totalPages,
                        hasPreviousPage = page > 1
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, "Er is een fout opgetreden bij het ophalen van gebruikers (admin)");
            }
        }

        /// <summary>
        /// PATCH /api/Users/{id}/make-admin - Maak een gebruiker admin (ADMIN only)
        /// </summary>
        [Authorize(Roles = "Admin")] 
        [HttpPatch("{id}/make-admin")]
        public async Task<IActionResult> MakeAdmin(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
                return NotFound("Gebruiker niet gevonden");

            user.Role = "Admin";
            await _context.SaveChangesAsync();

            return Ok("Gebruiker is nu admin ✅");
        }
    }
}