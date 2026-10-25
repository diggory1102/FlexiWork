using App.Application.Interfaces.Services;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace App.Infrastructure.Services
{
    public class FaceRecognitionService : IFaceRecognitionService
    {
        private readonly HttpClient _httpClient;
        private readonly string _aiBaseUrl;

        public FaceRecognitionService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _aiBaseUrl = configuration["AiService:BaseUrl"] ?? "http://localhost:8000";
        }

        public async Task<FaceExtractionResult> ExtractEmbeddingAsync(Stream imageStream, string fileName)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var streamContent = new StreamContent(imageStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                content.Add(streamContent, "file", fileName);

                var response = await _httpClient.PostAsync($"{_aiBaseUrl}/api/face/extract", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new FaceExtractionResult
                    {
                        Success = false,
                        Message = $"AI Service trả về lỗi: {response.StatusCode}"
                    };
                }

                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;
                bool success = root.GetProperty("success").GetBoolean();
                string message = root.GetProperty("message").GetString() ?? string.Empty;
                bool faceDetected = root.GetProperty("face_detected").GetBoolean();

                float[]? embedding = null;
                if (root.TryGetProperty("embedding", out var embElement) && embElement.ValueKind == JsonValueKind.Array)
                {
                    embedding = JsonSerializer.Deserialize<float[]>(embElement.GetRawText());
                }

                return new FaceExtractionResult
                {
                    Success = success,
                    Message = message,
                    FaceDetected = faceDetected,
                    Embedding = embedding
                };
            }
            catch (Exception ex)
            {
                return new FaceExtractionResult
                {
                    Success = false,
                    Message = $"Lỗi kết nối tới AI Service: {ex.Message}"
                };
            }
        }

        public async Task<FaceVerificationResult> VerifyFaceAsync(string targetEmbeddingJson, Stream checkinImageStream, string fileName)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                content.Add(new StringContent(targetEmbeddingJson), "target_embedding_json");

                using var streamContent = new StreamContent(checkinImageStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
                content.Add(streamContent, "file", fileName);

                var response = await _httpClient.PostAsync($"{_aiBaseUrl}/api/face/verify", content);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return new FaceVerificationResult
                    {
                        Success = false,
                        Message = $"AI Service trả về lỗi: {response.StatusCode}"
                    };
                }

                using var doc = JsonDocument.Parse(responseString);
                var root = doc.RootElement;

                return new FaceVerificationResult
                {
                    Success = root.GetProperty("success").GetBoolean(),
                    Message = root.GetProperty("message").GetString() ?? string.Empty,
                    IsMatched = root.GetProperty("is_matched").GetBoolean(),
                    Confidence = (float)root.GetProperty("confidence").GetDouble(),
                    Threshold = (float)root.GetProperty("threshold").GetDouble()
                };
            }
            catch (Exception ex)
            {
                return new FaceVerificationResult
                {
                    Success = false,
                    Message = $"Lỗi kết nối tới AI Service: {ex.Message}",
                    IsMatched = false
                };
            }
        }
    }
}
