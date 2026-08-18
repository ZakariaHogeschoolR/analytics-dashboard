namespace MobyParkApi.Models.Dto
{
    public class PdokRootDto
    {
        public PdokResponseDto response { get; set; }
    }

    public class PdokResponseDto
    {
        public int numFound { get; set; }
        public List<PdokDocAddressResponseDto> docs { get; set; }
    }

    public class PdokDocAddressResponseDto
    {
        public string straatnaam { get; set; }
        public string woonplaatsnaam { get; set; }
        public string postcode { get; set; }
        public int huisnummer { get; set; } // number ipv string
        public string weergavenaam { get; set; }
    }
}