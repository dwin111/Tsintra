using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Tsintra.Domain.Interfaces
{
    /// <summary>
    /// Repository interface for Prom.ua entities
    /// </summary>
    public interface IPromRepository
    {
        /// <summary>
        /// Saves a PromProduct with all its nested entities (Group, Category, Images)
        /// </summary>
        /// <param name="product">The product to save</param>
        /// <returns>ID of the saved product</returns>
        Task<long> SaveProductAsync(object product);
        
        /// <summary>
        /// Saves a PromGroup entity
        /// </summary>
        /// <param name="group">The group to save</param>
        /// <returns>ID of the saved group</returns>
        Task<long> SaveGroupAsync(object group);
        
        /// <summary>
        /// Saves a PromCategory entity
        /// </summary>
        /// <param name="category">The category to save</param>
        /// <returns>ID of the saved category</returns>
        Task<long> SaveCategoryAsync(object category);
        
        /// <summary>
        /// Saves PromImage entities for a product
        /// </summary>
        /// <param name="productId">The ID of the product</param>
        /// <param name="images">The images to save</param>
        /// <returns>Number of images saved</returns>
        Task<int> SaveImagesAsync(long productId, IEnumerable<object> images);
        
        /// <summary>
        /// Gets a PromProduct by its ID
        /// </summary>
        /// <param name="id">The ID of the product</param>
        /// <returns>The product with all its nested entities</returns>
        Task<object> GetProductByIdAsync(long id);
        
        /// <summary>
        /// Gets all PromProducts
        /// </summary>
        /// <returns>All products with their nested entities</returns>
        Task<IEnumerable<object>> GetAllProductsAsync();
        
        /// <summary>
        /// Gets a PromGroup by its ID
        /// </summary>
        /// <param name="id">The ID of the group</param>
        /// <returns>The group</returns>
        Task<object> GetGroupByIdAsync(long id);
        
        /// <summary>
        /// Gets all PromGroups
        /// </summary>
        /// <returns>All groups</returns>
        Task<IEnumerable<object>> GetAllGroupsAsync();
        
        /// <summary>
        /// Deletes a PromProduct by its ID
        /// </summary>
        /// <param name="id">The ID of the product</param>
        /// <returns>True if successful, false otherwise</returns>
        Task<bool> DeleteProductAsync(long id);
    }
} 