/** Biến môi trường dùng chung (đọc từ .env.local). */
export const env = {
  apiBaseUrl: import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5010/api',
  hubBaseUrl: import.meta.env.VITE_HUB_BASE_URL ?? 'http://localhost:5010/hubs',
};
