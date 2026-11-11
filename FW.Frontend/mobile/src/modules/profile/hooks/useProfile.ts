import { useCallback, useEffect, useState } from 'react';
import * as ImagePicker from 'expo-image-picker';
import { profileApi } from '@/services/profileApi';
import { getErrorMessage } from '@/services/apiClient';
import { useAuthStore } from '@/stores/authStore';
import type { UserProfile } from '@/types/user';

/** Giới hạn & định dạng ảnh đại diện — khớp ProfileService ở backend. */
const MAX_AVATAR_BYTES = 5 * 1024 * 1024;
const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

export type AvatarSource = 'camera' | 'library';

const pickerOptions: ImagePicker.ImagePickerOptions = {
  mediaTypes: ['images'],
  allowsEditing: true,
  aspect: [1, 1],
  quality: 0.7,
  // iOS: trả về JPEG thay vì HEIC (backend chỉ nhận JPG/PNG/WEBP)
  preferredAssetRepresentationMode: ImagePicker.UIImagePickerPreferredAssetRepresentationMode.Compatible,
};

/**
 * ViewModel màn hình Hồ sơ cá nhân (MVVM): tải hồ sơ, cập nhật số điện thoại, đổi ảnh đại diện.
 * Mọi thông báo trả về dạng { ok, message } để View tự quyết định cách hiển thị.
 */
export function useProfile() {
  const updateAuthUser = useAuthStore((s) => s.updateUser);
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [savingPhone, setSavingPhone] = useState(false);
  const [uploading, setUploading] = useState(false);

  const load = useCallback(async (mode: 'initial' | 'refresh' = 'initial') => {
    if (mode === 'refresh') setRefreshing(true);
    try {
      const { data } = await profileApi.get();
      setProfile(data);
      setError(null);
      updateAuthUser({ fullName: data.fullName, avatarUrl: data.avatarUrl, branchId: data.branchId, roles: data.roles });
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [updateAuthUser]);

  useEffect(() => {
    void load();
  }, [load]);

  const savePhone = async (phone: string) => {
    const value = phone.trim();
    // Khớp ràng buộc UpdateProfileRequest ở backend: chữ số, dấu + và khoảng trắng, tối đa 15 ký tự
    if (value && !/^[0-9+ ]{8,15}$/.test(value)) {
      return { ok: false, message: 'Số điện thoại gồm 8–15 ký tự: chữ số, dấu + và khoảng trắng.' };
    }
    setSavingPhone(true);
    try {
      const { data, message } = await profileApi.update(value || null);
      setProfile(data);
      return { ok: true, message };
    } catch (err) {
      return { ok: false, message: getErrorMessage(err) };
    } finally {
      setSavingPhone(false);
    }
  };

  /** Chọn/chụp ảnh rồi tải lên. Trả về null nếu người dùng hủy. */
  const changeAvatar = async (source: AvatarSource) => {
    const permission =
      source === 'camera'
        ? await ImagePicker.requestCameraPermissionsAsync()
        : await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!permission.granted) {
      return {
        ok: false,
        message: source === 'camera' ? 'Bạn cần cấp quyền camera trong Cài đặt.' : 'Bạn cần cấp quyền truy cập thư viện ảnh trong Cài đặt.',
      };
    }

    const result =
      source === 'camera' ? await ImagePicker.launchCameraAsync(pickerOptions) : await ImagePicker.launchImageLibraryAsync(pickerOptions);
    if (result.canceled || !result.assets[0]) return null;

    const asset = result.assets[0];
    const mimeType = asset.mimeType ?? 'image/jpeg';
    if (!ALLOWED_TYPES.includes(mimeType)) return { ok: false, message: 'Chỉ hỗ trợ ảnh JPG, PNG hoặc WEBP.' };
    if (asset.fileSize && asset.fileSize > MAX_AVATAR_BYTES) return { ok: false, message: 'Ảnh đại diện tối đa 5 MB.' };

    setUploading(true);
    try {
      const { data, message } = await profileApi.uploadAvatar({ uri: asset.uri, mimeType, fileName: asset.fileName });
      setProfile(data);
      updateAuthUser({ avatarUrl: data.avatarUrl });
      return { ok: true, message };
    } catch (err) {
      return { ok: false, message: getErrorMessage(err) };
    } finally {
      setUploading(false);
    }
  };

  return { profile, loading, refreshing, error, savingPhone, uploading, refresh: () => load('refresh'), savePhone, changeAvatar };
}
