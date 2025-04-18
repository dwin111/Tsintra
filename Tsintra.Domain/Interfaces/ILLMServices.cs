using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tsintra.Domain.Models;

namespace Tsintra.Domain.Interfaces
{
    /// <summary>
    /// Сервіс для роботи з LLM моделями
    /// </summary>
    public interface ILLMServices
    {
        /// <summary>
        /// Генерує відповідь на основі промпту
        /// </summary>
        /// <param name="prompt">Промпт для моделі</param>
        /// <param name="systemPrompt">Системний промпт (необов'язково)</param>
        /// <returns>Згенерований текст</returns>
        Task<string> GenerateResponseAsync(string prompt, string? systemPrompt = null);

        /// <summary>
        /// Генерує відповідь з використанням пам'яті розмови
        /// </summary>
        /// <param name="prompt">Промпт для моделі</param>
        /// <param name="conversationId">ID розмови (необов'язково)</param>
        /// <param name="userId">ID користувача (необов'язково)</param>
        /// <returns>Згенерований текст</returns>
        Task<string> GenerateTextWithMemoryAsync(string prompt, string? conversationId = null, Guid? userId = null);

        /// <summary>
        /// Отримує завершення тексту
        /// </summary>
        Task<string> GetCompletionAsync(string prompt, string? modelId = null);
        
        /// <summary>
        /// Отримує завершення розмови
        /// </summary>
        Task<string> GetChatCompletionAsync(string systemMessage, string userMessage, string? modelId = null);
        
        /// <summary>
        /// Отримує завершення розмови з повним списком повідомлень
        /// </summary>
        Task<string> GetChatCompletionAsync(List<Dictionary<string, string>> messages, string? modelId = null);
        
        /// <summary>
        /// Генерує опис зображення з файлу
        /// </summary>
        Task<string> GenerateImageDescriptionAsync(IFormFile imageFile, string? prompt = null);
        
        /// <summary>
        /// Генерує опис зображення з URL
        /// </summary>
        Task<string> GenerateImageDescriptionAsync(string imageUrl, string? prompt = null);
        
        /// <summary>
        /// Генерує зображення на основі опцій
        /// </summary>
        Task<byte[]> GenerateImageAsync(ImageOptions options);
    }
}
