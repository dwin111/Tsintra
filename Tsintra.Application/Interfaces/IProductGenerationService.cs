using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tsintra.Domain.DTOs;

namespace Tsintra.Application.Interfaces;

/// <summary>
/// Сервіс для генерації та публікації даних про продукти
/// </summary>
public interface IProductGenerationService
{
    /// <summary>
    /// Встановлює ідентифікатор користувача для генерації продукту
    /// </summary>
    /// <param name="userId">Ідентифікатор користувача</param>
    void SetUserId(Guid userId);
    
    /// <summary>
    /// Генерує опис та метадані продукту на основі зображень та підказок користувача
    /// </summary>
    /// <param name="base64Images">Зображення у форматі base64</param>
    /// <param name="userHints">Додаткові підказки від користувача (необов'язково)</param>
    /// <param name="cancellationToken">Токен для скасування операції</param>
    /// <returns>Об'єкт з інформацією про продукт</returns>
    Task<ProductDetailsDto?> GenerateProductAsync(
        IEnumerable<string> base64Images,
        string? userHints = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Публікує продукт на маркетплейсі
    /// </summary>
    /// <param name="productDetails">Інформація про продукт</param>
    /// <param name="cancellationToken">Токен для скасування операції</param>
    /// <returns>Результат публікації</returns>
    Task<PublishResultDto> PublishProductAsync(
        ProductDetailsDto productDetails,
        CancellationToken cancellationToken = default);
} 