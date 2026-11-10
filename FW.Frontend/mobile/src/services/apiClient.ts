import axios, { AxiosError } from 'axios';
import { env } from '@/config/env';
import { useAuthStore } from '@/stores/authStore';
import type { ApiResponse } from '@/types/api';

/** Axios instance dùng chung: tự gắn JWT, chuẩn hóa lỗi theo ApiResponse. */
export const apiClient = axios.create({
  baseURL: env.apiBaseUrl,
  timeout: 30_000,
});

apiClient.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError<ApiResponse<unknown>>) => {
    if (error.response?.status === 401) {
      useAuthStore.getState().logout();
    }
    const message =
      error.response?.data?.message ?? error.message ?? 'Không thể kết nối tới máy chủ.';
    return Promise.reject(new Error(message));
  },
);

/** Gọi API và trả về trực tiếp phần data của ApiResponse. */
export async function unwrap<T>(request: Promise<{ data: ApiResponse<T> }>): Promise<T> {
  const { data } = await request;
  if (!data.success) {
    throw new Error(data.message);
  }
  return data.data as T;
}
