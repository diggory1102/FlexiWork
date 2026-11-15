import { useCallback, useRef, useState } from 'react';
import { useFocusEffect } from 'expo-router';
import { attendanceApi } from '@/services/attendanceApi';
import { getErrorMessage } from '@/services/apiClient';
import { getDeviceId, LocationError, readPosition, readWifiBssid, type DevicePosition } from '@/lib/device';
import type { AttendanceCapture, AttendanceResult, AttendanceToday } from '@/types/attendance';

/** Vị trí & Wi-Fi đọc trước để hiển thị khoảng cách tới chi nhánh (máy chủ vẫn tự kiểm tra lại). */
export interface Probe {
  loading: boolean;
  position: DevicePosition | null;
  bssid: string | null;
  error: string | null;
  canOpenSettings: boolean;
}

const emptyProbe: Probe = { loading: false, position: null, bssid: null, error: null, canOpenSettings: false };

/**
 * ViewModel màn hình chấm công: tải việc cần làm hôm nay, đọc GPS / Wi-Fi / mã thiết bị, gửi ảnh selfie.
 * Lỗi trả về dạng chuỗi để View hiển thị.
 */
export function useAttendance() {
  const [today, setToday] = useState<AttendanceToday | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [probe, setProbe] = useState<Probe>(emptyProbe);
  const probing = useRef(false);
  const loaded = useRef(false);

  const runProbe = useCallback(async () => {
    if (probing.current) return;
    probing.current = true;
    setProbe((p) => ({ ...p, loading: true, error: null }));
    try {
      const position = await readPosition(); // xin quyền vị trí trước — Android cần quyền này mới đọc được BSSID
      const bssid = await readWifiBssid();
      setProbe({ loading: false, position, bssid, error: null, canOpenSettings: false });
    } catch (err) {
      setProbe({
        ...emptyProbe,
        error: err instanceof Error ? err.message : 'Không lấy được vị trí.',
        canOpenSettings: err instanceof LocationError && err.canOpenSettings,
      });
    } finally {
      probing.current = false;
    }
  }, []);

  const load = useCallback(
    async (mode: 'initial' | 'refresh' | 'silent' = 'initial') => {
      if (mode === 'refresh') setRefreshing(true);
      else if (mode === 'initial') setLoading(true);
      try {
        const { data } = await attendanceApi.today();
        setToday(data);
        setError(null);
        loaded.current = true;
        if (data.nextAction !== 'none') void runProbe();
      } catch (err) {
        setError(getErrorMessage(err));
      } finally {
        setLoading(false);
        setRefreshing(false);
      }
    },
    [runProbe],
  );

  // Tải lại mỗi khi quay lại tab (VD vừa đăng ký Face ID xong)
  useFocusEffect(
    useCallback(() => {
      void load(loaded.current ? 'silent' : 'initial');
    }, [load]),
  );

  /** Đọc vị trí mới nhất ngay trước khi chụp ảnh (không dùng kết quả đọc trước có thể đã cũ). */
  const prepareCapture = async (): Promise<{ ok: true; capture: AttendanceCapture } | { ok: false; message: string }> => {
    try {
      const position = await readPosition();
      const [bssid, deviceId] = await Promise.all([readWifiBssid(), getDeviceId()]);
      setProbe({ loading: false, position, bssid, error: null, canOpenSettings: false });
      return {
        ok: true,
        capture: {
          latitude: position.latitude,
          longitude: position.longitude,
          accuracy: position.accuracy,
          bssid,
          deviceId,
          isMocked: position.mocked,
        },
      };
    } catch (err) {
      return { ok: false, message: err instanceof Error ? err.message : 'Không lấy được vị trí.' };
    }
  };

  /** Gửi ảnh selfie; thành công thì tải lại trạng thái hôm nay. */
  const submit = async (action: 'check_in' | 'check_out', capture: AttendanceCapture, photoUri: string) => {
    try {
      const call = action === 'check_in' ? attendanceApi.checkIn : attendanceApi.checkOut;
      const { data } = await call(capture, photoUri);
      void load('silent');
      return { ok: true as const, result: data as AttendanceResult };
    } catch (err) {
      return { ok: false as const, message: getErrorMessage(err) };
    }
  };

  return {
    today,
    loading,
    refreshing,
    error,
    probe,
    reprobe: runProbe,
    refresh: () => load('refresh'),
    retry: () => load(),
    prepareCapture,
    submit,
  };
}
