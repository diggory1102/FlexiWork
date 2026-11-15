import { useCallback, useEffect, useState } from 'react';
import { profileApi } from '@/services/profileApi';
import { getErrorMessage } from '@/services/apiClient';
import type { FaceStatus } from '@/types/scheduling';

/** ViewModel Face ID: trạng thái đăng ký và gửi ảnh mẫu. Lỗi trả về dạng chuỗi để View hiển thị. */
export function useFaceId() {
  const [status, setStatus] = useState<FaceStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const { data } = await profileApi.faceStatus();
      setStatus(data);
      setError(null);
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  /** Gửi ảnh vừa chụp; thành công trả về thông báo, thất bại ném lỗi đã chuẩn hóa. */
  const register = async (uri: string): Promise<{ ok: boolean; message: string }> => {
    setSubmitting(true);
    try {
      const { data, message } = await profileApi.registerFace({ uri, mimeType: 'image/jpeg', fileName: 'face.jpg' });
      setStatus(data);
      return { ok: true, message };
    } catch (err) {
      return { ok: false, message: getErrorMessage(err) };
    } finally {
      setSubmitting(false);
    }
  };

  return { status, loading, error, submitting, reload: load, register };
}
