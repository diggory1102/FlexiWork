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
  async (error: AxiosError<ApiResponse<unknown>>) => {
    const status = error.response?.status;
    let body = error.response?.data;
    // Tải tệp (responseType: 'blob') lỗi → phần lỗi JSON của ApiResponse nằm trong Blob
    if (body instanceof Blob) {
      try {
        body = JSON.parse(await body.text()) as ApiResponse<unknown>;
      } catch {
        body = undefined;
      }
    }

    // Phiên hết hạn khi đang dùng (không áp dụng cho chính request đăng nhập)
    // VD: mật khẩu vừa được đổi trên thiết bị khác → máy chủ thu hồi token, trang đăng nhập hiện lý do
    if (status === 401 && !error.config?.url?.includes('/auth/login')) {
      const { accessToken, logout } = useAuthStore.getState();
      if (accessToken) logout(body?.message);
    }

    const message =
      body?.message ??
      (error.code === 'ERR_NETWORK' ? 'Không thể kết nối tới máy chủ. Vui lòng kiểm tra backend.' : error.message);
    return Promise.reject(new ApiError(message, status, body?.errors ?? []));
  },
);

/** Bỏ các tham số rỗng ('' / null / undefined) trước khi gửi query string. */
export function cleanParams<T extends object>(params: T): Partial<T> {
  return Object.fromEntries(
    Object.entries(params).filter(([, value]) => value !== '' && value !== null && value !== undefined),
  ) as Partial<T>;
}

async function request<T>(config: AxiosRequestConfig): Promise<{ data: T; message: string }> {
  const response = await apiClient.request<ApiResponse<T>>(config);
  const body = response.data;
  if (!body.success) {
    throw new ApiError(body.message, response.status, body.errors ?? []);
  }
  return { data: body.data as T, message: body.message };
}

/** Các hàm gọi API trả về trực tiếp phần data (và message khi cần hiển thị). */
export const api = {
  get: <T>(url: string, params?: object) => request<T>({ method: 'GET', url, params: params && cleanParams(params) }),
  post: <T>(url: string, data?: unknown, config?: AxiosRequestConfig) => request<T>({ method: 'POST', url, data, ...config }),
  put: <T>(url: string, data?: unknown) => request<T>({ method: 'PUT', url, data }),
  patch: <T>(url: string, data?: unknown) => request<T>({ method: 'PATCH', url, data }),
  delete: <T>(url: string) => request<T>({ method: 'DELETE', url }),
};

/** Lỗi dạng tiêu đề + danh sách chi tiết (hiển thị bằng ErrorList). */
export function getErrorDetail(error: unknown): { message: string; errors: string[] } {
  if (error instanceof ApiError) return { message: error.message, errors: error.errors };
  return { message: error instanceof Error ? error.message : 'Đã xảy ra lỗi không xác định.', errors: [] };
}

/** Lấy nội dung lỗi để hiển thị (thông báo + chi tiết). */
export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    return error.errors.length > 0 ? `${error.message} ${error.errors.join(' ')}` : error.message;
  }
  return error instanceof Error ? error.message : 'Đã xảy ra lỗi không xác định.';
}

/** Tên tệp trong header Content-Disposition (ưu tiên filename*=UTF-8''...). */
function fileNameFrom(disposition: string | undefined): string | null {
  if (!disposition) return null;
  const encoded = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(disposition);
  if (encoded) {
    try {
      return decodeURIComponent(encoded[1].trim().replace(/^"|"$/g, ''));
    } catch {
      /* dùng filename thường */
    }
  }
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain ? plain[1].trim() : null;
}

/** Tải tệp từ API (có JWT) rồi cho trình duyệt lưu; trả về tên tệp. Lỗi được chuẩn hóa như các request khác. */
export async function downloadFile(url: string, params?: object): Promise<string> {
  const response = await apiClient.get<Blob>(url, { params: params && cleanParams(params), responseType: 'blob', timeout: 120_000 });
  const name = fileNameFrom(response.headers['content-disposition']) ?? 'tai-xuong';
  const objectUrl = URL.createObjectURL(response.data);
  const link = document.createElement('a');
  link.href = objectUrl;
  link.download = name;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(objectUrl);
  return name;
}
