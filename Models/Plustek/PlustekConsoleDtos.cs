using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlustekBCR.Models.Plustek
{
    public class ProjectTokenResponse
    {
        [JsonPropertyName("data")]
        public ProjectTokenData? Data { get; set; }
    }

    public class ProjectTokenData
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
    }

    public class CreateUploadUrlResponse
    {
        [JsonPropertyName("data")]
        public CreateUploadUrlData? Data { get; set; }
    }

    public class CreateUploadUrlData
    {
        [JsonPropertyName("urls")]
        public List<UploadUrlItem> Urls { get; set; } = new();
    }

    public class UploadUrlItem
    {
        [JsonPropertyName("file_id")]
        public string FileId { get; set; } = string.Empty;

        [JsonPropertyName("signed_url")]
        public string SignedUrl { get; set; } = string.Empty;
    }

    public class ConfirmUploadResponse
    {
        [JsonPropertyName("data")]
        public ConfirmUploadData? Data { get; set; }
    }

    public class ConfirmUploadData
    {
        [JsonPropertyName("files")]
        public List<ConfirmedFileItem> Files { get; set; } = new();
    }

    public class ConfirmedFileItem
    {
        [JsonPropertyName("file_id")]
        public string FileId { get; set; } = string.Empty;
    }

    public class CreateDocumentResponse
    {
        [JsonPropertyName("data")]
        public CreateDocumentData? Data { get; set; }
    }

    public class CreateDocumentData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    public class CreateJobResponse
    {
        [JsonPropertyName("data")]
        public CreateJobData? Data { get; set; }
    }

    public class CreateJobData
    {
        [JsonPropertyName("job_id")]
        public string JobId { get; set; } = string.Empty;
    }

    public class EscanJobResultResponse
    {
        [JsonPropertyName("data")]
        public JsonElement Data { get; set; }
    }
}
