using System.Text.Json.Serialization;

namespace PlustekBCR.Models
{
    public class ZipCloudResponse
    {
        [JsonPropertyName("message")]
        public string? Message { get; set; }

        [JsonPropertyName("results")]
        public ZipCloudResult[]? Results { get; set; }

        [JsonPropertyName("status")]
        public int Status { get; set; }
    }

    public class ZipCloudResult
    {
        [JsonPropertyName("address1")]
        public string? Address1 { get; set; }

        [JsonPropertyName("address2")]
        public string? Address2 { get; set; }

        [JsonPropertyName("address3")]
        public string? Address3 { get; set; }

        [JsonPropertyName("zipcode")]
        public string? Zipcode { get; set; }
    }
}
