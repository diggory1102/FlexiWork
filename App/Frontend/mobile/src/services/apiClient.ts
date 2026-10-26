// Base API client configuration for Mobile (React Native / Expo)
const BASE_URL = 'http://localhost:5247/api';

export const apiClient = {
  get: async <T>(endpoint: string, token?: string): Promise<T> => {
    const res = await fetch(`${BASE_URL}${endpoint}`, {
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
    });
    if (!res.ok) throw new Error(`HTTP error ${res.status}`);
    return res.json();
  },
  post: async <T>(endpoint: string, body: any, token?: string): Promise<T> => {
    const isFormData = body instanceof FormData;
    const res = await fetch(`${BASE_URL}${endpoint}`, {
      method: 'POST',
      headers: {
        ...(isFormData ? {} : { 'Content-Type': 'application/json' }),
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: isFormData ? body : JSON.stringify(body),
    });
    if (!res.ok) throw new Error(`HTTP error ${res.status}`);
    return res.json();
  },
};
