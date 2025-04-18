using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tsintra.Domain.DTOs;

namespace Tsintra.Domain.Interfaces
{
    /// <summary>
    /// Інтерфейс для інструментів генерації продуктів (замість ListingAgent)
    /// </summary>
    public interface IProductGenerationTools
    {
        /// <summary>
        /// Встановлює ідентифікатор користувача клієнта
        /// </summary>
        /// <param name="userId">Ідентифікатор користувача</param>
        void SetClientUserId(Guid userId);

        /// <summary>
        /// Генерує інформацію про продукт на основі зображень та підказок користувача
        /// </summary>
        /// <param name="base64Images">Список зображень у форматі base64</param>
        /// <param name="userHints">Підказки користувача (необов'язково)</param>
        /// <param name="conversationId">Ідентифікатор розмови (необов'язково)</param>
        /// <param name="cancellationToken">Токен скасування</param>
        /// <returns>Дані про продукт</returns>
        Task<ProductDetailsDto?> GenerateProductAsync(
            IEnumerable<string> base64Images,
            string? userHints = null,
            string? conversationId = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Публікує продукт на маркетплейсі
        /// </summary>
        /// <param name="productDetails">Дані про продукт</param>
        /// <param name="conversationId">Ідентифікатор розмови (необов'язково)</param>
        /// <param name="cancellationToken">Токен скасування</param>
        /// <returns>Результат публікації</returns>
        Task<PublishResultDto> PublishProductAsync(
            ProductDetailsDto productDetails,
            string? conversationId = null,
            CancellationToken cancellationToken = default);
    }
} 