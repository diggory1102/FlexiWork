import { useRef, useState } from 'react';
import { ActivityIndicator, Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { CameraView } from 'expo-camera';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { colors, radius } from '@/theme';

/** Chọn cỡ ảnh nhỏ nhất có cạnh dài ≥ 1280 px (đủ cho nhận diện, ảnh gửi đi nhẹ). */
function pickPictureSize(sizes: string[]): string | undefined {
  const parsed = sizes
    .map((s) => ({ s, m: /^(\d+)x(\d+)$/.exec(s) }))
    .filter((x): x is { s: string; m: RegExpExecArray } => !!x.m)
    .map(({ s, m }) => ({ s, long: Math.max(Number(m[1]), Number(m[2])) }))
    .sort((a, b) => a.long - b.long);
  return (parsed.find((p) => p.long >= 1280) ?? parsed.at(-1))?.s;
}

interface SelfieCameraProps {
  /** Dòng hướng dẫn trên cùng. */
  hint?: string;
  onCapture: (uri: string) => void;
  onCancel: () => void;
  onError: (message: string) => void;
}

/**
 * Camera trước toàn màn hình có khung căn mặt và nút chụp — dùng cho đăng ký Face ID và ảnh selfie chấm công.
 * Màn hình gọi tới phải xin quyền camera trước khi hiển thị component này.
 */
export function SelfieCamera({ hint = 'Đặt khuôn mặt vào giữa khung', onCapture, onCancel, onError }: SelfieCameraProps) {
  const cameraRef = useRef<CameraView>(null);
  // Tránh tai thỏ / thanh điều hướng khi camera chiếm toàn màn hình (không có tiêu đề, thanh tab)
  const insets = useSafeAreaInsets();
  const [ready, setReady] = useState(false);
  const [pictureSize, setPictureSize] = useState<string>();
  const [capturing, setCapturing] = useState(false);

  const onCameraReady = async () => {
    setReady(true);
    if (Platform.OS === 'web' || pictureSize) return;
    try {
      const size = pickPictureSize((await cameraRef.current?.getAvailablePictureSizesAsync()) ?? []);
      if (size) setPictureSize(size);
    } catch {
      // Thiết bị không hỗ trợ liệt kê cỡ ảnh → dùng mặc định
    }
  };

  const capture = async () => {
    if (!cameraRef.current || capturing) return;
    setCapturing(true);
    try {
      const picture = await cameraRef.current.takePictureAsync({ quality: 0.8, shutterSound: false });
      onCapture(picture.uri);
    } catch {
      onError('Không chụp được ảnh. Vui lòng thử lại.');
    } finally {
      setCapturing(false);
    }
  };

  return (
    <View style={styles.cameraScreen}>
      <CameraView
        ref={cameraRef}
        style={StyleSheet.absoluteFill}
        facing="front"
        pictureSize={pictureSize}
        onCameraReady={onCameraReady}
        onMountError={() => onError('Không mở được camera. Vui lòng thử lại.')}
      />
      <View style={[styles.overlay, { paddingTop: Math.max(32, insets.top + 16), paddingBottom: Math.max(40, insets.bottom + 24) }]} pointerEvents="box-none">
        <Text style={styles.overlayText}>{hint}</Text>
        <View style={styles.oval} />
        <View style={styles.cameraActions}>
          <Pressable onPress={onCancel} style={styles.cameraSide} accessibilityRole="button" accessibilityLabel="Hủy">
            <Text style={styles.cameraSideText}>Hủy</Text>
          </Pressable>
          <Pressable
            onPress={capture}
            disabled={!ready || capturing}
            style={({ pressed }) => [styles.shutter, (!ready || capturing) && styles.disabled, pressed && styles.pressed]}
            accessibilityRole="button"
            accessibilityLabel="Chụp ảnh"
          >
            {capturing ? <ActivityIndicator color={colors.primary} /> : <View style={styles.shutterInner} />}
          </Pressable>
          <View style={styles.cameraSide} />
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  cameraScreen: { flex: 1, backgroundColor: '#000' },
  overlay: { position: 'absolute', top: 0, right: 0, bottom: 0, left: 0, alignItems: 'center', justifyContent: 'space-between' },
  overlayText: { color: colors.white, fontSize: 16, fontWeight: '600', backgroundColor: 'rgba(0,0,0,0.45)', paddingHorizontal: 14, paddingVertical: 6, borderRadius: radius.full },
  oval: { width: '68%', aspectRatio: 0.78, borderRadius: 999, borderWidth: 3, borderColor: 'rgba(255,255,255,0.9)' },
  cameraActions: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', width: '100%', paddingHorizontal: 32 },
  cameraSide: { width: 64, alignItems: 'center' },
  cameraSideText: { color: colors.white, fontSize: 16, fontWeight: '600' },
  shutter: { width: 76, height: 76, borderRadius: 38, borderWidth: 4, borderColor: colors.white, alignItems: 'center', justifyContent: 'center' },
  shutterInner: { width: 58, height: 58, borderRadius: 29, backgroundColor: colors.white },
  disabled: { opacity: 0.5 },
  pressed: { opacity: 0.8 },
});
