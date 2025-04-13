using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization; // Cần cho JsonPropertyName
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using EmailChatASP.Controllers; // Giữ lại nếu lớp ChatRequest ở đây
using System.Collections.Generic; // Cần cho List

namespace EmailChatASP.Services
{
    // --- Các lớp để biểu diễn cấu trúc JSON kiểu OpenAI ---
    public class OpenAIChatMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    public class OpenAIChatRequest
    {
        [JsonPropertyName("model")]
        public string Model { get; set; } // Có thể không bắt buộc với URL này, nhưng nên có

        [JsonPropertyName("messages")]
        public List<OpenAIChatMessage> Messages { get; set; }

        [JsonPropertyName("max_tokens")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] // Bỏ qua nếu null
        public int? MaxTokens { get; set; }

        // Thêm các tham số khác nếu cần (temperature, top_p, stream...)
        // [JsonPropertyName("temperature")]
        // [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        // public double? Temperature { get; set; }
    }

    public class OpenAIResponseMessage
    {
        [JsonPropertyName("role")]
        public string Role { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; }
    }

    public class OpenAIChatChoice
    {
        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("message")]
        public OpenAIResponseMessage Message { get; set; }

        [JsonPropertyName("finish_reason")]
        public string FinishReason { get; set; }
    }

    public class OpenAIChatCompletionResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("created")]
        public long Created { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("choices")]
        public List<OpenAIChatChoice> Choices { get; set; }

        // Có thể có thêm "usage" property
        // [JsonPropertyName("usage")]
        // public object Usage { get; set; }
    }
    // --- Kết thúc các lớp biểu diễn cấu trúc JSON ---


    public class ChatService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ChatService> _logger;
        private readonly string _hfApiKey;
        private readonly HttpClient _httpClient;
        private readonly string _huggingFaceApiUrl; // Lưu URL router ở đây
        private readonly string _modelName; // Lưu tên model nếu cần

        public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IHttpClientFactory httpClientFactory)
        {
            _configuration = configuration;
            _logger = logger;

            _hfApiKey = _configuration["HuggingFace:ApiKey"];
            // *** Đảm bảo URL này là URL bạn vừa cung cấp ***
            _huggingFaceApiUrl = _configuration["HuggingFace:ApiUrl"];
            // Lấy tên model từ URL hoặc cấu hình (ví dụ: mistralai/Mistral-7B-Instruct-v0.3)
            _modelName = _configuration["HuggingFace:ModelName"] ?? "mistralai/Mistral-7B-Instruct-v0.3";

            if (string.IsNullOrEmpty(_hfApiKey))
            {
                _logger.LogError("Hugging Face API Key is missing in configuration (Checked 'HuggingFace:ApiKey').");
            }
            if (string.IsNullOrEmpty(_huggingFaceApiUrl))
            {
                _logger.LogError("Hugging Face API URL (Router) is missing in configuration (Checked 'HuggingFace:ApiUrl').");
            }

            _httpClient = httpClientFactory.CreateClient("HuggingFaceClient");
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _hfApiKey);
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public async Task<string> GetResponse(ChatRequest chatRequest)
        {
            if (string.IsNullOrEmpty(_hfApiKey) || string.IsNullOrEmpty(_huggingFaceApiUrl))
            {
                _logger.LogError("Attempted to use ChatService without proper API Key or URL configuration.");
                return "Sorry, the chatbot is not configured correctly.";
            }
            if (string.IsNullOrWhiteSpace(chatRequest?.Message))
            {
                _logger.LogWarning("Received an empty chat request message.");
                return "Please provide a message.";
            }

            try
            {
                _logger.LogInformation("Sending message to Hugging Face Router API ({ModelUrl}): '{Message}'", _huggingFaceApiUrl, chatRequest.Message);

                // --- Gọi Hugging Face Router API (kiểu OpenAI) ---

                // 1. Chuẩn bị Payload theo cấu trúc OpenAI
                var requestPayload = new OpenAIChatRequest
                {
                    Model = _modelName, // Gửi tên model
                    Messages = new List<OpenAIChatMessage>
                    {
                        // TODO: Trong tương lai, bạn có thể thêm các tin nhắn cũ vào đây để duy trì ngữ cảnh
                        new OpenAIChatMessage { Role = "user", Content = chatRequest.Message }
                    },
                    MaxTokens = 250 // Giới hạn token trả về (tùy chỉnh)
                    // Temperature = 0.7 // Điều chỉnh độ "sáng tạo"
                };
                string jsonPayload = JsonSerializer.Serialize(requestPayload);

                // 2. Tạo Request Content
                using var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                // 3. Gửi Request POST đến URL Router
                using var response = await _httpClient.PostAsync(_huggingFaceApiUrl, content);

                // 4. Xử lý Response
                string responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Received successful response from Hugging Face Router API.");

                    // 5. Parse JSON Response theo cấu trúc OpenAI
                    try
                    {
                        // Sử dụng JsonSerializer để deserialize vào đối tượng C#
                        var openAIResponse = JsonSerializer.Deserialize<OpenAIChatCompletionResponse>(responseBody);

                        // Kiểm tra và lấy nội dung trả lời từ assistant
                        if (openAIResponse?.Choices != null && openAIResponse.Choices.Count > 0)
                        {
                            var firstChoice = openAIResponse.Choices[0];
                            if (firstChoice.Message != null && !string.IsNullOrEmpty(firstChoice.Message.Content))
                            {
                                return firstChoice.Message.Content.Trim();
                            }
                        }

                        // Nếu không tìm thấy nội dung mong đợi
                        _logger.LogWarning("Could not find 'choices[0].message.content' in Hugging Face Router response. Raw Response: {ResponseBody}", responseBody);
                        return "Received a response, but couldn't extract the answer.";
                    }
                    catch (JsonException jsonEx)
                    {
                        _logger.LogError(jsonEx, "Failed to parse OpenAI-compatible JSON response from Hugging Face: {ResponseBody}", responseBody);
                        return "Received an invalid response format from the AI.";
                    }
                }
                else // Xử lý lỗi HTTP
                {
                    _logger.LogError("Error calling Hugging Face Router API. Status: {StatusCode}, Body: {ErrorBody}", response.StatusCode, responseBody);

                    string errorMessage = $"Sorry, failed to get response from AI (Error: {response.StatusCode}).";
                    try
                    {
                        // Thử parse lỗi kiểu OpenAI { "error": { "message": "...", "type": ..., } }
                        using JsonDocument errorDoc = JsonDocument.Parse(responseBody);
                        if (errorDoc.RootElement.TryGetProperty("error", out JsonElement errorObject) &&
                            errorObject.TryGetProperty("message", out JsonElement messageElement))
                        {
                            errorMessage += $" Details: {messageElement.GetString()}";
                        }
                    }
                    catch { } // Bỏ qua nếu không parse được lỗi

                    if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                    { // 401
                        errorMessage = "Sorry, there's an issue with the chatbot configuration (API Key).";
                    }
                    else if ((int)response.StatusCode == 429)
                    { // 429 Too Many Requests
                        errorMessage = "Sorry, the free usage limit for the AI might have been reached. Please try again later.";
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                    { // 503
                        errorMessage = "The AI model might be loading or unavailable. Please try again in a moment.";
                    }
                    return errorMessage;
                }
                // --- Kết thúc gọi Hugging Face Router API ---
            }
            catch (HttpRequestException httpEx) // Lỗi mạng
            {
                _logger.LogError(httpEx, "Network error calling Hugging Face API.");
                return "Sorry, couldn't connect to the AI service. Please check your internet connection.";
            }
            catch (TaskCanceledException cancelEx) when (!cancelEx.CancellationToken.IsCancellationRequested) // Timeout
            {
                _logger.LogError(cancelEx, "The request to Hugging Face API timed out.");
                return "Sorry, the request to the AI timed out. Please try again.";
            }
            catch (Exception ex) // Các lỗi khác
            {
                _logger.LogError(ex, "An unexpected error occurred in ChatService while contacting Hugging Face.");
                return "Sorry, an internal error occurred while processing your request.";
            }
        }
    }
}