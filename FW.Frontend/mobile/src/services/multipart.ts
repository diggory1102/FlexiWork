import { Platform } from 'react-native';

/** Ảnh đã chọn từ thư viện / camera. */
export interface PickedImage {
  uri: string;
  mimeType?: string | null;
  fileName?: string | null;
}

/**
 * Tạo FormData gồm file ảnh (trường "file") và các trường chữ. React Native nhận object { uri, name, type };
 * bản web cần Blob thật nên phải tải nội dung từ uri trước.
 */
export async function imageFormData(image: PickedImage, fields: Record<string, string | number | boolean | null | undefined> = {}): Promise<FormData> {
  const type = image.mimeType ?? 'image/jpeg';
  const extension = type.split('/')[1] ?? 'jpg';
  const name = image.fileName ?? `image.${extension}`;
  const form = new FormData();

  for (const [key, value] of Object.entries(fields)) {
    if (value !== null && value !== undefined) form.append(key, String(value));
  }

  if (Platform.OS === 'web') {
    const blob = await (await fetch(image.uri)).blob();
    form.append('file', blob, name);
  } else {
    form.append('file', { uri: image.uri, name, type } as unknown as Blob);
  }
  return form;
}

/** Native: axios cần khai báo multipart và giữ nguyên FormData; web: để trình duyệt tự đặt boundary. */
export const multipartConfig =
  Platform.OS === 'web' ? undefined : { headers: { 'Content-Type': 'multipart/form-data' }, transformRequest: (data: unknown) => data };
