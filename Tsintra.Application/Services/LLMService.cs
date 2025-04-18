using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Tsintra.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Tsintra.Domain.Models;
using System.Collections.Generic;

namespace Tsintra.Application.Services
{
    public class LLMService : ILLMService
    {
        private readonly HttpClient _httpClient;
        private readonly string _aiEndpoint;
        private readonly string _aiApiKey;
        private readonly string _chatModel;

        public LLMService(
            IConfiguration configuration,
            HttpClient httpClient)
        {
            _httpClient = httpClient;
            _aiEndpoint = configuration["OpenAI:Endpoint"] ?? throw new ArgumentNullException("OpenAI:Endpoint is not configured");
            _aiApiKey = configuration["OpenAI:ApiKey"] ?? throw new ArgumentNullException("OpenAI:ApiKey is not configured");
            _chatModel = configuration["OpenAI:ChatModel"] ?? throw new ArgumentNullException("OpenAI:ChatModel is not configured");
        }

        public async Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null)
        {
            var request = new
            {
                model = _chatModel,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt ?? "You are a helpful assistant." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.7,
                max_tokens = 1000
            };

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _aiApiKey);
            var response = await _httpClient.PostAsync(_aiEndpoint, 
                new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"AI API request failed with status code: {response.StatusCode}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var aiResponse = JsonSerializer.Deserialize<AIResponse>(responseContent);
            
            return aiResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim() ?? 
                   throw new Exception("Failed to generate response");
        }

        public async Task<string> GenerateTextWithMemoryAsync(string prompt, string? systemPrompt = null, Guid? memoryId = null)
        {
            // TODO: Implement memory integration
            return await GenerateResponseAsync(prompt, systemPrompt);
        }

        public async Task<string> DescribeImagesAsync(string prompt, List<IFormFile> images, List<string> imageUrls)
        {
            throw new NotImplementedException("Image description is not implemented yet");
        }

        public async Task<string> GenerateImageAsync(ImageOptions options)
        {
            throw new NotImplementedException("Image generation is not implemented yet");
        }

        public async Task<List<string>> ConvertImageSourcesAsync(List<IFormFile> images, List<string> imageUrls)
        {
            throw new NotImplementedException("Image source conversion is not implemented yet");
        }

        private class AIResponse
        {
            public List<Choice>? Choices { get; set; }
        }

        private class Choice
        {
            public Message? Message { get; set; }
        }

        private class Message
        {
            public string? Content { get; set; }
        }
    }
} 