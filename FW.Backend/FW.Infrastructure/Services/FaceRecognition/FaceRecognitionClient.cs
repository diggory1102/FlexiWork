using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FW.Application.Common.Interfaces;

namespace FW.Infrastructure.Services.FaceRecognition;

/// <summary>
/// HTTP client gọi sang FW.AiService (FastAPI) để trích xuất và đối soát khuôn mặt.
/// </summary>
public class FaceRecognitionClient : IFaceRecognitionService
{
    // FW.AiService trả JSON dạng snake_case (face_detected, is_matched, ...)
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;

    public FaceRecognitionClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<FaceExtractResult> ExtractEmbeddingAsync(Stream image, string fileName, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(CreateImageContent(image), "file", fileName);

        using var response = await _httpClient.PostAsync("api/face/extract", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FaceExtractResult>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("FW.AiService trả về dữ liệu rỗng.");
    }

    public async Task<FaceVerifyResult> VerifyAsync(Stream image, string fileName, IReadOnlyList<float> targetEmbedding, CancellationToken cancellationToken = default)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(JsonSerializer.Serialize(targetEmbedding)), "target_embedding_json");
        form.Add(CreateImageContent(image), "file", fileName);

        using var response = await _httpClient.PostAsync("api/face/verify", form, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<FaceVerifyResult>(JsonOptions, cancellationToken)
               ?? throw new InvalidOperationException("FW.AiService trả về dữ liệu rỗng.");
    }

    private static StreamContent CreateImageContent(Stream image)
    {
        var content = new StreamContent(image);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return content;
    }
}
