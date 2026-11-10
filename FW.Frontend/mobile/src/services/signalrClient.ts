import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { env } from '@/config/env';
import { useAuthStore } from '@/stores/authStore';

/**
 * Tạo kết nối SignalR tới một Hub của backend (ví dụ: "notifications", "chat").
 * Token JWT được gửi qua query string access_token (backend đã hỗ trợ cho đường dẫn /hubs).
 */
export function createHubConnection(hubName: string): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${env.hubBaseUrl}/${hubName}`, {
      accessTokenFactory: () => useAuthStore.getState().accessToken ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(__DEV__ ? LogLevel.Information : LogLevel.Warning)
    .build();
}
