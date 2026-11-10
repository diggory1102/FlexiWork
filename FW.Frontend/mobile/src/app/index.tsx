import { StyleSheet, Text, View } from 'react-native';

/**
 * Màn hình chính (tạm thời).
 * Sẽ được thay bằng luồng đăng nhập + các tab: Chấm công, Lịch ca, Đơn từ, Công việc, Tin nhắn, Cá nhân.
 */
export default function HomeScreen() {
  return (
    <View style={styles.container}>
      <Text style={styles.title}>FlexiWork</Text>
      <Text style={styles.subtitle}>Ứng dụng chấm công dành cho nhân viên</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: '#F8FAFC', padding: 16 },
  title: { fontSize: 24, fontWeight: '600', color: '#0F172A' },
  subtitle: { marginTop: 4, fontSize: 14, color: '#64748B' },
});
