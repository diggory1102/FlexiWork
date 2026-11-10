/** Biến môi trường (Expo chỉ nhúng các biến có tiền tố EXPO_PUBLIC_). */
export const env = {
  apiBaseUrl: process.env.EXPO_PUBLIC_API_BASE_URL ?? 'http://localhost:5010/api',
  hubBaseUrl: process.env.EXPO_PUBLIC_HUB_BASE_URL ?? 'http://localhost:5010/hubs',
};
