import { Platform } from 'react-native';
import * as Location from 'expo-location';
import * as Application from 'expo-application';
import NetInfo from '@react-native-community/netinfo';

// iOS chỉ trả SSID/BSSID khi bật cờ này (và ứng dụng có quyền "Access Wi-Fi Information" — bản build riêng, không có trong Expo Go)
NetInfo.configure({ shouldFetchWiFiSSID: true });

export interface DevicePosition {
  latitude: number;
  longitude: number;
  /** Sai số (m) hệ điều hành báo cáo. */
  accuracy: number | null;
  /** Hệ điều hành báo vị trí đến từ ứng dụng giả lập (Android). */
  mocked: boolean;
}

/** Lỗi đọc vị trí có thông báo tiếng Việt để hiển thị thẳng cho người dùng. */
export class LocationError extends Error {
  constructor(
    message: string,
    public readonly canOpenSettings = false,
  ) {
    super(message);
  }
}

const POSITION_TIMEOUT_MS = 20_000;

/** Xin quyền và đọc vị trí hiện tại với độ chính xác cao (không dùng vị trí cũ đã lưu). */
export async function readPosition(): Promise<DevicePosition> {
  const permission = await Location.requestForegroundPermissionsAsync();
  if (!permission.granted) {
    throw new LocationError('Bạn cần cho phép FlexiWork dùng vị trí để chấm công.', !permission.canAskAgain);
  }
  if (Platform.OS !== 'web' && !(await Location.hasServicesEnabledAsync())) {
    throw new LocationError('Định vị (GPS) đang tắt. Hãy bật định vị rồi thử lại.');
  }

  const position = await Promise.race([
    Platform.OS === 'web' ? readBrowserPosition() : Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.High }),
    new Promise<never>((_, reject) =>
      setTimeout(() => reject(new LocationError('Không lấy được vị trí GPS. Hãy ra chỗ thoáng rồi thử lại.')), POSITION_TIMEOUT_MS),
    ),
  ]);

  return {
    latitude: position.coords.latitude,
    longitude: position.coords.longitude,
    accuracy: position.coords.accuracy ?? null,
    mocked: position.mocked === true,
  };
}

/** Bản web (xem thử khi phát triển): expo-location trả vị trí đã lưu (maximumAge = ∞) nên đọc trực tiếp vị trí mới. */
function readBrowserPosition(): Promise<Location.LocationObject> {
  return new Promise((resolve, reject) => {
    navigator.geolocation.getCurrentPosition(
      (p) =>
        resolve({
          timestamp: p.timestamp,
          coords: {
            latitude: p.coords.latitude,
            longitude: p.coords.longitude,
            accuracy: p.coords.accuracy,
            altitude: p.coords.altitude,
            altitudeAccuracy: p.coords.altitudeAccuracy,
            heading: p.coords.heading,
            speed: p.coords.speed,
          },
        }),
      () => reject(new LocationError('Không lấy được vị trí. Hãy cho phép trình duyệt dùng vị trí.')),
      { enableHighAccuracy: true, maximumAge: 0, timeout: POSITION_TIMEOUT_MS },
    );
  });
}

/**
 * BSSID (địa chỉ MAC router) của Wi-Fi đang kết nối; null nếu không dùng Wi-Fi hoặc hệ điều hành không cho đọc.
 * Android cần quyền vị trí (xin ở {@link readPosition}); iOS cần bản build riêng có quyền Wi-Fi.
 */
export async function readWifiBssid(): Promise<string | null> {
  try {
    const state = await NetInfo.fetch('wifi');
    if (state.type !== 'wifi' || !state.isConnected) return null;
    const bssid = state.details?.bssid ?? null;
    // Android trả địa chỉ giả này khi không được phép đọc BSSID thật
    return bssid && bssid !== '02:00:00:00:00:00' ? bssid : null;
  } catch {
    return null;
  }
}

/** Mã thiết bị ổn định (Android ID / iOS identifierForVendor) — để phát hiện một điện thoại chấm công cho nhiều người. */
export async function getDeviceId(): Promise<string | null> {
  try {
    if (Platform.OS === 'android') return Application.getAndroidId();
    if (Platform.OS === 'ios') return await Application.getIosIdForVendorAsync();
  } catch {
    // Không đọc được → bỏ qua (máy chủ vẫn chấm công, chỉ không đối chiếu được trùng thiết bị)
  }
  return null;
}

/** Khoảng cách Haversine (m) — để hiển thị trước; máy chủ tự tính lại khi chấm công. */
export function distanceMeters(lat1: number, lng1: number, lat2: number, lng2: number): number {
  const R = 6_371_008.8;
  const rad = (d: number) => (d * Math.PI) / 180;
  const dPhi = rad(lat2 - lat1);
  const dLambda = rad(lng2 - lng1);
  const a = Math.sin(dPhi / 2) ** 2 + Math.cos(rad(lat1)) * Math.cos(rad(lat2)) * Math.sin(dLambda / 2) ** 2;
  return 2 * R * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
}

/** "35 m" / "1,2 km". */
export function formatDistance(meters: number): string {
  // Khoảng trắng không ngắt dòng: số và đơn vị luôn đi cùng nhau
  return meters >= 1000 ? `${(meters / 1000).toFixed(1).replace('.', ',')} km` : `${Math.round(meters)} m`;
}
