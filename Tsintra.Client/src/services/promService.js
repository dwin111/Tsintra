import axios from 'axios';

const API_URL = process.env.REACT_APP_API_URL || 'https://localhost:5001';

/**
 * Сервіс для роботи з API Prom.UA
 */
class PromService {
  /**
   * Отримати всі товари з Prom.UA
   * @returns {Promise<Array>} масив товарів
   */
  async getProducts() {
    try {
      const response = await axios.get(`${API_URL}/api/prom/products`);
      return response.data;
    } catch (error) {
      console.error('Error getting products from Prom.UA:', error);
      throw error;
    }
  }

  /**
   * Отримати товар за ID
   * @param {string} marketplaceProductId ID товару в Prom.UA
   * @returns {Promise<Object>} товар
   */
  async getProductById(marketplaceProductId) {
    try {
      const response = await axios.get(`${API_URL}/api/prom/products/${marketplaceProductId}`);
      return response.data;
    } catch (error) {
      console.error(`Error getting product from Prom.UA with ID ${marketplaceProductId}:`, error);
      throw error;
    }
  }

  /**
   * Створити новий товар в Prom.UA
   * @param {Object} product дані товару
   * @returns {Promise<Object>} створений товар
   */
  async createProduct(product) {
    try {
      const response = await axios.post(`${API_URL}/api/prom/products`, product);
      return response.data;
    } catch (error) {
      console.error('Error creating product in Prom.UA:', error);
      throw error;
    }
  }

  /**
   * Оновити товар в Prom.UA
   * @param {string} marketplaceProductId ID товару в Prom.UA
   * @param {Object} product дані товару
   * @returns {Promise<boolean>} результат оновлення
   */
  async updateProduct(marketplaceProductId, product) {
    try {
      await axios.put(`${API_URL}/api/prom/products/${marketplaceProductId}`, product);
      return true;
    } catch (error) {
      console.error(`Error updating product in Prom.UA with ID ${marketplaceProductId}:`, error);
      throw error;
    }
  }

  /**
   * Видалити товар з Prom.UA
   * @param {string} marketplaceProductId ID товару в Prom.UA
   * @returns {Promise<boolean>} результат видалення
   */
  async deleteProduct(marketplaceProductId) {
    try {
      await axios.delete(`${API_URL}/api/prom/products/${marketplaceProductId}`);
      return true;
    } catch (error) {
      console.error(`Error deleting product from Prom.UA with ID ${marketplaceProductId}:`, error);
      throw error;
    }
  }

  /**
   * Імпортувати товари з Prom.UA в локальну базу даних
   * @returns {Promise<Object>} результат імпорту
   */
  async importProducts() {
    try {
      const response = await axios.post(`${API_URL}/api/prom/products/import`);
      return response.data;
    } catch (error) {
      console.error('Error importing products from Prom.UA:', error);
      throw error;
    }
  }

  /**
   * Експортувати товари з локальної бази даних в Prom.UA
   * @param {Array<string>} productIds масив ID товарів для експорту
   * @returns {Promise<Object>} результат експорту
   */
  async exportProducts(productIds = null) {
    try {
      const response = await axios.post(`${API_URL}/api/prom/products/export`, productIds);
      return response.data;
    } catch (error) {
      console.error('Error exporting products to Prom.UA:', error);
      throw error;
    }
  }

  /**
   * Синхронізувати товари між локальною базою даних і Prom.UA
   * @param {string} direction напрямок синхронізації ('import', 'export', 'both')
   * @param {Array<string>} productIds масив ID товарів для синхронізації
   * @returns {Promise<Object>} результат синхронізації
   */
  async syncProducts(direction = 'both', productIds = null) {
    try {
      const response = await axios.post(
        `${API_URL}/api/prom/products/sync?direction=${direction}`, 
        productIds
      );
      return response.data;
    } catch (error) {
      console.error('Error syncing products with Prom.UA:', error);
      throw error;
    }
  }

  /**
   * Імпортувати товари з файлу і опційно опублікувати їх на Prom.UA
   * @param {File} file файл з товарами
   * @param {boolean} publishToProm публікувати товари на Prom.UA
   * @returns {Promise<Object>} результат імпорту
   */
  async importProductsFromFile(file, publishToProm = false) {
    try {
      const formData = new FormData();
      formData.append('file', file);
      
      const response = await axios.post(
        `${API_URL}/api/prom/products/import-from-file?publishToProm=${publishToProm}`, 
        formData,
        {
          headers: {
            'Content-Type': 'multipart/form-data'
          }
        }
      );
      return response.data;
    } catch (error) {
      console.error('Error importing products from file:', error);
      throw error;
    }
  }
}

export default new PromService(); 