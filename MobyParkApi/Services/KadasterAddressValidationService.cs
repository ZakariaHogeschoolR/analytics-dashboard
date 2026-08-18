using MobyParkApi.Models.Dto;
using System.Text.Json;

namespace MobyParkApi.Service
{
    public interface IAddressValidationService
    {
        Task<bool> AddressExistsAsync(string postcode, int houseNumber);
        Task<PdokDocAddressResponseDto?> GetAddressAsync(string postcode, int houseNumber);
    }

    public class KadasterAddressValidationService : IAddressValidationService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<KadasterAddressValidationService> _logger;

        public KadasterAddressValidationService(
            HttpClient httpClient,
            ILogger<KadasterAddressValidationService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "MobyParkApi/1.0 (contact: tech@mobypark.nl)");
        }

        public async Task<bool> AddressExistsAsync(string postcode, int houseNumber)
        {
            var query = $"{postcode} {houseNumber}";
            var url =
                $"https://api.pdok.nl/bzk/locatieserver/search/v3_1/free" +
                $"?q={Uri.EscapeDataString(query)}&fq=type:adres&rows=1";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PDOK call failed: {StatusCode}", response.StatusCode);
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();

            // Simpel maar effectief
            return json.Contains("\"numFound\":1");
        }

        public async Task<PdokDocAddressResponseDto?> GetAddressAsync(string postcode, int houseNumber)
        {
            var query = $"{postcode} {houseNumber}";
            var url = $"https://api.pdok.nl/bzk/locatieserver/search/v3_1/free?q={Uri.EscapeDataString(query)}&fq=type:adres&rows=1";

            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("PDOK call failed: {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();

            var pdokRoot = JsonSerializer.Deserialize<PdokRootDto>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (pdokRoot?.response?.numFound > 0)
                return pdokRoot.response.docs.FirstOrDefault();

            return null;
        }
    }
}