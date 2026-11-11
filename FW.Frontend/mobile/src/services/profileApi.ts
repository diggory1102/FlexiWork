import { Platform } from 'react-native';
import { api } from './apiClient';
import type { UserProfile } from '@/types/user';

/** Ảnh đã chọn từ thư viện / camera (expo-image-picker). */
export interface PickedImage {
  uri: string;
  mimeType?: string | null;
  fileName?: string | null;
}

/**
 * Tạo FormData cho file ảnh. React Native nhận object { uri, name, type };
 * bản web cần Blob thật nên phải tải nội dung từ uri trước.
 */
async function toFormData(image: PickedImage): Promise<FormData> {
  const type = image.mimeType ?? 'image/jpeg';
  const extension = type.split('/')[1] ?? 'jpg';
  const name = image.fileName ?? `avatar.${extension}`;
  const form = new FormData();

  if (Platform.OS === 'web') {
    const blob = await (await fetch(image.uri)).blob();
    form.append('file', blob, name);
  } else {
    form.append('file', { uri: image.uri, name, type } as unknown as Blob);
  }
  return form;
}

export const profileApi = {
  get: () => api.get<UserProfile>('/profile'),
  update: (phoneNumber: string | null) => api.put<UserProfile>('/profile', { phoneNumber }),
  uploadAvatar: async (image: PickedImage) =>
    api.post<UserProfile>(
      '/profile/avatar',
      await toFormData(image),
      // Native: axios cần khai báo multipart và giữ nguyên FormData; web: để trình duyệt tự đặt boundary.
      Platform.OS === 'web' ? undefined : { headers: { 'Content-Type': 'multipart/form-data' }, transformRequest: (data) => data },
    ),
};
