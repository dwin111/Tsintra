using System;
using Tsintra.Domain.Models;

namespace Tsintra.Api.Crm.Models
{
    public static class ProductExtensions
    {
        /// <summary>
        /// Gets the quantity from QuantityInStock
        /// </summary>
        public static int Quantity(this Product product)
        {
            return product.QuantityInStock ?? 0;
        }

        /// <summary>
        /// Sets the quantity to QuantityInStock
        /// </summary>
        public static void SetQuantity(this Product product, int quantity)
        {
            product.QuantityInStock = quantity;
        }
    }
} 