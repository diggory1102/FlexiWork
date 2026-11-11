import { Alert, Platform } from 'react-native';

/**
 * Hộp thoại hệ thống. Alert của React Native không hoạt động trên bản web (chỉ dùng để xem thử
 * giao diện khi phát triển), nên bản web dùng alert/confirm của trình duyệt.
 */
export function notify(message: string, title = 'Thông báo') {
  if (Platform.OS === 'web') globalThis.alert?.(message);
  else Alert.alert(title, message);
}

export function confirmAction(title: string, message: string, confirmText: string, onConfirm: () => void, destructive = false) {
  if (Platform.OS === 'web') {
    if (globalThis.confirm?.(`${title}\n\n${message}`)) onConfirm();
    return;
  }
  Alert.alert(title, message, [
    { text: 'Hủy', style: 'cancel' },
    { text: confirmText, style: destructive ? 'destructive' : 'default', onPress: onConfirm },
  ]);
}
