import axios, { AxiosError, type AxiosRequestConfig } from 'axios';
import { env } from '@/config/env';
import { useAuthStore } from '@/stores/authStore';
import type { ApiResponse } from '@/types/api';

/** Lỗi gọi API đã chuẩn hóa: thông báo chính + danh sách lỗi chi tiết từ ApiResponse. */
export class ApiError extends Error {
  constructor(
    message: string,
    public readonly status: number | undefined,
    public readonly errors: string[] = [],
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

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
    const status = error.response?.status;
    const body = error.response?.data;

    // Phiên hết hạn / bị thu hồi khi đang dùng (không áp dụng cho chính request đăng nhập)
    if (status === 401 && !error.config?.url?.includes('/auth/login')) {
      useAuthStore.getState().logout();
    }

    const message =
      body?.message ??
      (error.code === 'ERR_NETWORK' || error.code === 'ECONNABORTED'
        ? 'Không thể kết nối tới máy chủ. Vui lòng kiểm tra mạng hoặc địa chỉ máy chủ.'
        : error.message);
    return Promise.reject(new ApiError(message, status, body?.errors ?? []));
  },
);

async function request<T>(config: AxiosRequestConfig): Promise<{ data: T; message: string }> {
  const response = await apiClient.request<ApiResponse<T>>(config);
  const body = response.data;
  if (!body.success) {
    throw new ApiError(body.message, response.status, body.errors ?? []);
  }
  return { data: body.data as T, message: body.message };
}

/** Các hàm gọi API trả về trực tiếp phần data (và message để hiển thị). */
export const api = {
  get: <T>(url: string, params?: object) => request<T>({ method: 'GET', url, params }),
  post: <T>(url: string, data?: unknown, config?: AxiosRequestConfig) => request<T>({ method: 'POST', url, data, ...config }),
  put: <T>(url: string, data?: unknown) => request<T>({ method: 'PUT', url, data }),
  patch: <T>(url: string, data?: unknown) => request<T>({ method: 'PATCH', url, data }),
  delete: <T>(url: string) => request<T>({ method: 'DELETE', url }),
};

/** Lấy nội dung lỗi để hiển thị (thông báo + chi tiết). */
export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? `${error.message} ${error.errors.join(' ')}` : error.message;
  }
  return error instanceof Error ? error.message : 'Đã xảy ra lỗi không xác định.';
}
