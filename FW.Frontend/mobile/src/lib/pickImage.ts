import * as ImagePicker from 'expo-image-picker';
import type { PickedImage } from '@/services/multipart';

const MAX_BYTES = 5 * 1024 * 1024;

const options: ImagePicker.ImagePickerOptions = {
  mediaTypes: ['images'],
  quality: 0.6,
  // iOS: trả về JPEG thay vì HEIC để mọi trình xem ảnh đều mở được
  preferredAssetRepresentationMode: ImagePicker.UIImagePickerPreferredAssetRepresentationMode.Compatible,
};

export type PickResult = { ok: true; image: PickedImage } | { ok: false; message: string } | null;

/**
 * Chụp ảnh (camera) hoặc chọn từ thư viện cho ảnh minh chứng (đơn từ, checklist). null = người dùng hủy.
 * Ảnh quá 5 MB bị từ chối ngay trên máy (backend cũng giới hạn 5 MB).
 */
export async function pickImage(source: 'camera' | 'library'): Promise<PickResult> {
  const permission =
    source === 'camera' ? await ImagePicker.requestCameraPermissionsAsync() : await ImagePicker.requestMediaLibraryPermissionsAsync();
  if (!permission.granted) {
    return { ok: false, message: source === 'camera' ? 'Bạn cần cấp quyền camera trong Cài đặt.' : 'Bạn cần cấp quyền thư viện ảnh trong Cài đặt.' };
  }

  const result = source === 'camera' ? await ImagePicker.launchCameraAsync(options) : await ImagePicker.launchImageLibraryAsync(options);
  if (result.canceled || !result.assets[0]) return null;
  const asset = result.assets[0];
  if (asset.fileSize && asset.fileSize > MAX_BYTES) return { ok: false, message: 'Ảnh tối đa 5 MB — hãy chụp lại hoặc chọn ảnh khác.' };
  return { ok: true, image: { uri: asset.uri, mimeType: asset.mimeType ?? 'image/jpeg', fileName: asset.fileName ?? 'minh-chung.jpg' } };
}
