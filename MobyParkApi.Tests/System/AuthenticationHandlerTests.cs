using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

namespace MobyParkApi.Tests
{
	public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
	{
		public const string Scheme = "TestAuth";

		public TestAuthHandler(
			IOptionsMonitor<AuthenticationSchemeOptions> options,
			ILoggerFactory logger,
			UrlEncoder encoder,
			ISystemClock clock)
			: base(options, logger, encoder, clock)
		{ }

		protected override Task<AuthenticateResult> HandleAuthenticateAsync()
		{
			var claims = new[]
			{
				new Claim(ClaimTypes.NameIdentifier, "1"),
				new Claim(ClaimTypes.Name, "testuser"),
				new Claim(ClaimTypes.Role, "User")
			};
			var identity = new ClaimsIdentity(claims, Scheme);
			var principal = new ClaimsPrincipal(identity);
			var ticket = new AuthenticationTicket(principal, Scheme);
			return Task.FromResult(AuthenticateResult.Success(ticket));
		}
	}
}


