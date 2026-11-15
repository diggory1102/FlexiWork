import { useState, type ComponentProps } from 'react';
import { ActivityIndicator, Image, Linking, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useCameraPermissions } from 'expo-camera';
import { Ionicons } from '@expo/vector-icons';
import { Card, Notice } from '@/components/ui/Feedback';
import { Button } from '@/components/ui/Button';
import { SelfieCamera } from '@/components/camera/SelfieCamera';
import { formatDate } from '@/lib/format';
import { notify } from '@/lib/dialog';
import { colors, radius } from '@/theme';
import { useFaceId } from '../useFaceId';

type Step = 'intro' | 'camera' | 'preview';

const tips: [ComponentProps<typeof Ionicons>['name'], string][] = [
  ['person-outline', 'Chỉ một mình bạn trong khung hình'],
  ['sunny-outline', 'Đủ sáng, không ngược sáng'],
  ['glasses-outline', 'Không đeo khẩu trang, kính râm, mũ che mặt'],
  ['scan-outline', 'Nhìn thẳng, đặt khuôn mặt vào giữa khung hướng dẫn'],
];

/**
 * Đăng ký khuôn mặt mẫu (UC 2.4) bằng camera trước. Chỉ đăng ký được một lần; muốn đổi phải nhờ Quản lý / HR đặt lại
 * (chống tự đổi sang khuôn mặt người khác để nhờ chấm công hộ).
 */
export function FaceRegisterScreen() {
  const vm = useFaceId();
  const [permission, requestPermission] = useCameraPermissions();
  const [step, setStep] = useState<Step>('intro');
  const [photo, setPhoto] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const startCamera = async () => {
    setError(null);
    const result = permission?.granted ? permission : await requestPermission();
    if (!result.granted) {
      setError('Bạn cần cho phép FlexiWork dùng camera để đăng ký khuôn mặt.');
      return;
    }
    setStep('camera');
  };

  const submit = async () => {
    if (!photo) return;
    setError(null);
    const result = await vm.register(photo);
    if (result.ok) {
      setPhoto(null);
      setStep('intro');
      notify(result.message, 'Thành công');
    } else {
      setError(result.message);
    }
  };

  if (vm.loading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }

  if (!vm.status) {
    return (
      <View style={[styles.center, styles.padded]}>
        <Notice tone="danger" message={vm.error ?? 'Không tải được trạng thái khuôn mặt.'} style={styles.stretch} />
        <Button title="Thử lại" variant="secondary" onPress={vm.reload} style={[styles.stretch, styles.mt16]} />
      </View>
    );
  }

  // ----- Đã đăng ký -----
  if (vm.status.registered) {
    return (
      <ScrollView style={styles.screen} contentContainerStyle={styles.content}>
        <Card style={styles.registeredCard}>
          {vm.status.sampleImageUrl ? (
            <Image source={{ uri: vm.status.sampleImageUrl }} style={styles.sample} accessibilityLabel="Ảnh khuôn mặt đã đăng ký" />
          ) : (
            <View style={[styles.sample, styles.samplePlaceholder]}>
              <Ionicons name="person" size={56} color={colors.muted} />
            </View>
          )}
          <View style={styles.okRow}>
            <Ionicons name="checkmark-circle" size={22} color={colors.success} />
            <Text style={styles.okText}>Đã đăng ký khuôn mặt</Text>
          </View>
          <Text style={styles.muted}>Ngày đăng ký: {formatDate(vm.status.registeredAt)}</Text>
        </Card>
        <Notice
          tone="info"
          message="Khuôn mặt này dùng để xác minh khi chấm công. Muốn đăng ký lại (VD đổi kiểu tóc, đeo kính), hãy nhờ Quản lý chi nhánh hoặc bộ phận Nhân sự đặt lại dữ liệu khuôn mặt."
        />
      </ScrollView>
    );
  }

  // ----- Camera -----
  if (step === 'camera') {
    return (
      <SelfieCamera
        onCapture={(uri) => {
          setPhoto(uri);
          setStep('preview');
        }}
        onCancel={() => setStep('intro')}
        onError={(message) => {
          setError(message);
          setStep('intro');
        }}
      />
    );
  }

  // ----- Xem lại ảnh -----
  if (step === 'preview' && photo) {
    return (
      <ScrollView style={styles.screen} contentContainerStyle={styles.content}>
        <Text style={styles.title}>Kiểm tra ảnh</Text>
        <Text style={styles.muted}>Khuôn mặt rõ nét, nhìn thẳng và chỉ có một người?</Text>
        <Image source={{ uri: photo }} style={styles.preview} resizeMode="cover" accessibilityLabel="Ảnh vừa chụp" />
        <Notice tone="danger" message={error} />
        <Button title="Dùng ảnh này" onPress={submit} loading={vm.submitting} />
        <Button title="Chụp lại" variant="secondary" onPress={() => void startCamera()} disabled={vm.submitting} />
      </ScrollView>
    );
  }

  // ----- Hướng dẫn -----
  return (
    <ScrollView style={styles.screen} contentContainerStyle={styles.content}>
      <View style={styles.hero}>
        <View style={styles.heroIcon}>
          <Ionicons name="scan" size={40} color={colors.primary} />
        </View>
        <Text style={styles.title}>Đăng ký khuôn mặt</Text>
        <Text style={[styles.muted, styles.centerText]}>
          Khuôn mặt mẫu dùng để xác minh lớp 3 khi chấm công, chống chấm công hộ. Bạn chỉ cần đăng ký một lần.
        </Text>
      </View>
      <Card style={styles.tips}>
        {tips.map(([icon, text]) => (
          <View key={text} style={styles.tipRow}>
            <Ionicons name={icon} size={20} color={colors.primary} />
            <Text style={styles.tipText}>{text}</Text>
          </View>
        ))}
      </Card>
      <Notice tone="danger" message={error} />
      {permission && !permission.granted && !permission.canAskAgain ? (
        <Button title="Mở Cài đặt để cấp quyền camera" variant="secondary" onPress={() => void Linking.openSettings()} />
      ) : (
        <Button title="Bắt đầu chụp" onPress={() => void startCamera()} icon={<Ionicons name="camera" size={18} color={colors.white} />} />
      )}
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.background },
  content: { padding: 16, paddingBottom: 32, gap: 16 },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background },
  padded: { padding: 24 },
  stretch: { alignSelf: 'stretch' },
  mt16: { marginTop: 16 },
  title: { fontSize: 20, fontWeight: '800', color: colors.foreground, textAlign: 'center' },
  muted: { fontSize: 14, lineHeight: 20, color: colors.muted },
  centerText: { textAlign: 'center' },
  hero: { alignItems: 'center', gap: 8, paddingTop: 8 },
  heroIcon: { width: 80, height: 80, borderRadius: 40, backgroundColor: colors.primarySoft, alignItems: 'center', justifyContent: 'center' },
  tips: { gap: 14 },
  tipRow: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  tipText: { flex: 1, fontSize: 14, color: colors.foreground },
  registeredCard: { alignItems: 'center', gap: 10, paddingVertical: 24 },
  sample: { width: 160, height: 200, borderRadius: radius.lg, backgroundColor: colors.background },
  samplePlaceholder: { alignItems: 'center', justifyContent: 'center' },
  okRow: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  okText: { fontSize: 17, fontWeight: '700', color: colors.success },
  preview: { width: '100%', aspectRatio: 3 / 4, borderRadius: radius.lg, backgroundColor: colors.border },
});
