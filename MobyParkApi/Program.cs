using Microsoft.EntityFrameworkCore;
using MobyParkApi.Data;
using MobyParkApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using MobyParkApi.Service;
using MobyParkApi.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// Controllers
builder.Services.AddControllers();

// CORS - staat de React frontend (Vite dev server) toe om de API te benaderen
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontendDev", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Database
if (builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("TestDb"));
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
}

// Services
builder.Services.AddScoped<IUsersService, UsersService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IProfileService, ProfileService>();
builder.Services.AddScoped<ReservationService>();
builder.Services.AddScoped<IArchiveService, ArchiveService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IPaymentGenerationService, PaymentGenerationService>();
builder.Services.AddScoped<IReservationAutoCompleteService, ReservationAutoCompleteService>();
builder.Services.AddScoped<IInvoiceArchiveService, InvoiceArchiveService>();
builder.Services.AddScoped<IInvoiceService, InvoiceService>();
builder.Services.AddScoped<IDiscountCodeService, DiscountCodeService>();
builder.Services.AddHttpClient<IAddressValidationService, KadasterAddressValidationService>();
builder.Services.AddHttpClient();
builder.Services.AddHostedService<ReservationToPaymentService>();


// JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)
            )
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var dbContext = context.HttpContext.RequestServices
                                .GetRequiredService<ApplicationDbContext>();
                
                var userIdClaim = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
                {
                    context.Fail("Gebruiker niet gevonden in token");
                    return;
                }

                var user = await dbContext.Users.FindAsync(userId);

                if (user == null || user.Active == false)
                {
                    context.Fail("Account is gedeactiveerd");
                    return;
                }
            }
        };
    });

// Authorization
builder.Services.AddAuthorization(options =>
{
    // standaard policy voor alleen ingelogde gebruikers
    options.DefaultPolicy = options.DefaultPolicy;

    // Alleen voor admin rol
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

    // Admin of user rol
    options.AddPolicy("UserOrAdmin", policy => policy.RequireRole("Admin", "User"));
});

// Swagger + Bearer Support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Voer hieronder je JWT in: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    c.OperationFilter<SwaggerOrderOperationFilter>();

    c.OrderActionsBy(api =>
    {
        // Sorteer eerst op custom order, dan op controllernaam
        var hasOrder = api.ActionDescriptor.EndpointMetadata
            .OfType<SwaggerOrderAttribute>()
            .FirstOrDefault();

        return $"{hasOrder?.Order:000}_{api.ActionDescriptor.RouteValues["controller"]}_{api.HttpMethod}";
    });
});

var app = builder.Build();

// ✅ Swagger middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ✅ CORS moet vóór authenticatie
app.UseCors("AllowFrontendDev");

// ✅ Zorg dat JWT werkt!
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();


public partial class Program { }