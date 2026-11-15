import { api } from './apiClient';
import { imageFormData, multipartConfig, type PickedImage } from './multipart';
import type { UserProfile } from '@/types/user';
import type { FaceStatus } from '@/types/scheduling';

export type { PickedImage } from './multipart';

export const profileApi = {
  get: () => api.get<UserProfile>('/profile'),
  update: (phoneNumber: string | null) => api.put<UserProfile>('/profile', { phoneNumber }),
  uploadAvatar: async (image: PickedImage) =>
    api.post<UserProfile>('/profile/avatar', await imageFormData({ fileName: 'avatar.jpg', ...image }), multipartConfig),

  /** Trạng thái đăng ký khuôn mặt (Face ID) của tôi. */
  faceStatus: () => api.get<FaceStatus>('/profile/face'),
  /** Đăng ký khuôn mặt mẫu — chỉ được một lần, muốn đổi phải nhờ Quản lý / HR đặt lại. */
  registerFace: async (image: PickedImage) => api.post<FaceStatus>('/profile/face', await imageFormData(image), multipartConfig),
};
