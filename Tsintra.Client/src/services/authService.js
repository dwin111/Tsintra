import axios from 'axios';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';

// Local storage keys
const TOKEN_KEY = 'auth_token';
const REFRESH_TOKEN_KEY = 'refresh_token';
const USER_DATA_KEY = 'user_data';

/**
 * Service for handling authentication and JWT tokens
 */
const authService = {
  /**
   * Log in with email and password
   * @param {string} email - User email
   * @param {string} password - User password
   * @returns {Promise} - Promise with user data
   */
  async login(email, password) {
    try {
      const response = await axios.post(`${API_URL}/auth/login`, { email, password });
      this.setSession(response.data);
      return response.data;
    } catch (error) {
      console.error('Login error:', error);
      throw error;
    }
  },

  /**
   * Log out the current user
   */
  logout() {
    this.clearSession();
  },

  /**
   * Refresh the access token using the refresh token
   * @returns {Promise} - Promise with new token data
   */
  async refreshToken() {
    try {
      const token = localStorage.getItem(TOKEN_KEY);
      const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
      
      if (!token || !refreshToken) {
        throw new Error('No tokens available');
      }

      const response = await axios.post(`${API_URL}/auth/refresh-token`, {
        token,
        refreshToken,
      });

      this.setSession(response.data);
      return response.data;
    } catch (error) {
      console.error('Token refresh error:', error);
      this.clearSession();
      throw error;
    }
  },

  /**
   * Revoke the current refresh token
   * @returns {Promise} - Promise with revocation result
   */
  async revokeToken() {
    try {
      const refreshToken = localStorage.getItem(REFRESH_TOKEN_KEY);
      
      if (!refreshToken) {
        throw new Error('No refresh token available');
      }

      const response = await axios.post(
        `${API_URL}/auth/revoke`,
        { refreshToken },
        {
          headers: {
            Authorization: `Bearer ${localStorage.getItem(TOKEN_KEY)}`,
          },
        }
      );
      
      this.clearSession();
      return response.data;
    } catch (error) {
      console.error('Token revocation error:', error);
      this.clearSession();
      throw error;
    }
  },

  /**
   * Get the current user data from local storage
   * @returns {Object|null} - User data or null if not logged in
   */
  getCurrentUser() {
    const userJson = localStorage.getItem(USER_DATA_KEY);
    if (!userJson) return null;
    try {
      return JSON.parse(userJson);
    } catch (e) {
      console.error('Error parsing user data:', e);
      return null;
    }
  },

  /**
   * Check if the user is authenticated
   * @returns {boolean} - True if user is authenticated
   */
  isAuthenticated() {
    return !!localStorage.getItem(TOKEN_KEY);
  },

  /**
   * Set up authentication session with tokens and user data
   * @param {Object} data - Session data containing token, refreshToken, etc.
   */
  setSession(data) {
    if (data.token) {
      localStorage.setItem(TOKEN_KEY, data.token);
      localStorage.setItem(REFRESH_TOKEN_KEY, data.refreshToken);
      
      // Extract and store user data
      const userData = {
        id: data.userId,
        name: data.name,
        email: data.email,
      };
      
      localStorage.setItem(USER_DATA_KEY, JSON.stringify(userData));
      
      // Set default authorization header for all requests
      axios.defaults.headers.common['Authorization'] = `Bearer ${data.token}`;
    }
  },

  /**
   * Clear the authentication session
   */
  clearSession() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(USER_DATA_KEY);
    delete axios.defaults.headers.common['Authorization'];
  },

  /**
   * Setup axios interceptor to handle token refresh
   */
  setupInterceptors() {
    axios.interceptors.response.use(
      (response) => response,
      async (error) => {
        const originalRequest = error.config;
        
        // If the error is not 401 or the request already tried to refresh, reject
        if (error.response.status !== 401 || originalRequest._retry) {
          return Promise.reject(error);
        }
        
        originalRequest._retry = true;
        
        try {
          // Try to refresh the token
          await this.refreshToken();
          
          // Update authorization header with new token
          originalRequest.headers['Authorization'] = `Bearer ${localStorage.getItem(TOKEN_KEY)}`;
          
          // Retry the original request
          return axios(originalRequest);
        } catch (refreshError) {
          // If refresh fails, clear session and redirect to login
          this.clearSession();
          window.location.href = '/login';
          return Promise.reject(refreshError);
        }
      }
    );
  },
};

// Initialize axios interceptors
authService.setupInterceptors();

export default authService; 