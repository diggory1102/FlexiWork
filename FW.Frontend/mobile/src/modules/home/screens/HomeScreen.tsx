import { useCallback, useEffect, useState, type ComponentProps } from 'react';
import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router, useFocusEffect, type Href } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { Avatar } from '@/components/ui/Avatar';
import { Badge, Card } from '@/components/ui/Feedback';
import { useAuthStore } from '@/stores/authStore';
import { formatLongDate, greeting } from '@/lib/format';
import { todayIso } from '@/lib/dates';
import { scheduleApi } from '@/services/scheduleApi';
import { profileApi } from '@/services/profileApi';
import { scheduleStatusBadges, shiftHours } from '@/modules/shifts/shiftUi';
import type { Schedule } from '@/types/scheduling';
import { colors, radius } from '@/theme';

type IconName = ComponentProps<typeof Ionicons>['name'];

const shortcuts: { icon: IconName; label: string; href: Href; tint: string }[] = [
  { icon: 'calendar-outline', label: 'Lịch ca', href: '/schedule', tint: '#2563EB' },
  { icon: 'document-text-outline', label: 'Đơn từ', href: '/requests', tint: '#D97706' },
  { icon: 'checkbox-outline', label: 'Công việc', href: '/tasks', tint: '#16A34A' },
  { icon: 'chatbubbles-outline', label: 'Tin nhắn', href: '/chat', tint: '#7C3AED' },
];

/** Trang chủ nhân viên: lời chào, đồng hồ, ca hôm nay, nút chấm công nhanh và lối tắt. */
export function HomeScreen() {
  const user = useAuthStore((s) => s.user);
  const now = useClock();
  const { shifts, faceRegistered } = useToday();
  if (!user) return null;

  const time = `${String(now.getHours()).padStart(2, '0')}:${String(now.getMinutes()).padStart(2, '0')}`;

  return (
    <SafeAreaView edges={['top']} style={styles.safe}>
      <ScrollView contentContainerStyle={styles.content} showsVerticalScrollIndicator={false}>
        <View style={styles.header}>
          <View style={styles.headerText}>
            <Text style={styles.greeting}>{greeting(now)},</Text>
            <Text style={styles.name} numberOfLines={1}>
              {user.fullName}
            </Text>
          </View>
          <Pressable onPress={() => router.push('/profile')} accessibilityLabel="Hồ sơ cá nhân">
            <Avatar name={user.fullName} url={user.avatarUrl} size={48} />
          </Pressable>
        </View>

        <View style={styles.clockCard}>
          <Text style={styles.date}>{formatLongDate(now)}</Text>
          <Text style={styles.clock}>{time}</Text>
          <Pressable style={styles.checkIn} onPress={() => router.push('/attendance')} accessibilityRole="button">
            <Ionicons name="finger-print" size={22} color={colors.primary} />
            <Text style={styles.checkInText}>Chấm công</Text>
          </Pressable>
          <Text style={styles.clockHint}>Xác minh 3 lớp: GPS · Wi-Fi nội bộ · Khuôn mặt</Text>
        </View>

        {faceRegistered === false && (
          <Pressable onPress={() => router.push('/face-id')} style={({ pressed }) => [styles.faceReminder, pressed && styles.pressed]} accessibilityRole="button">
            <Ionicons name="scan-outline" size={22} color={colors.warning} />
            <View style={styles.flex}>
              <Text style={styles.faceTitle}>Bạn chưa đăng ký khuôn mặt</Text>
              <Text style={styles.shiftText}>Cần đăng ký một lần để chấm công. Chạm để đăng ký ngay.</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.warning} />
          </Pressable>
        )}

        <Text style={styles.section}>Ca làm hôm nay</Text>
        {shifts === null || shifts.length === 0 ? (
          <Card style={styles.shiftCard}>
            <View style={styles.shiftIcon}>
              <Ionicons name="cafe-outline" size={22} color={colors.muted} />
            </View>
            <View style={styles.flex}>
              <Text style={styles.shiftTitle}>{shifts === null ? 'Đang tải lịch…' : 'Hôm nay bạn không có ca'}</Text>
              <Text style={styles.shiftText}>Xem lịch cả tuần ở mục Lịch ca.</Text>
            </View>
          </Card>
        ) : (
          shifts.map((s) => {
            const badge = scheduleStatusBadges[s.status];
            return (
              <Pressable key={s.id} onPress={() => router.push('/schedule')} style={({ pressed }) => [pressed && styles.pressed]}>
                <Card style={[styles.shiftCard, styles.shiftSpacing]}>
                  <View style={[styles.shiftIcon, styles.shiftIconActive]}>
                    <Ionicons name="time-outline" size={22} color={colors.primary} />
                  </View>
                  <View style={styles.flex}>
                    <Text style={[styles.shiftTitle, s.status === 'cancelled' && styles.cancelled]}>{s.shiftName}</Text>
                    <Text style={styles.shiftText}>{shiftHours(s)}</Text>
                  </View>
                  {badge && <Badge label={badge.label} tone={badge.tone} />}
                </Card>
              </Pressable>
            );
          })
        )}

        <Text style={styles.section}>Tiện ích</Text>
        <View style={styles.grid}>
          {shortcuts.map((s) => (
            <Pressable key={s.label} style={({ pressed }) => [styles.tile, pressed && styles.pressed]} onPress={() => router.push(s.href)}>
              <View style={[styles.tileIcon, { backgroundColor: `${s.tint}1A` }]}>
                <Ionicons name={s.icon} size={22} color={s.tint} />
              </View>
              <Text style={styles.tileLabel}>{s.label}</Text>
            </Pressable>
          ))}
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

/** Ca hôm nay + trạng thái Face ID, tải lại mỗi lần quay về Trang chủ. */
function useToday() {
  const [shifts, setShifts] = useState<Schedule[] | null>(null);
  const [faceRegistered, setFaceRegistered] = useState<boolean | null>(null);

  useFocusEffect(
    useCallback(() => {
      const today = todayIso();
      scheduleApi
        .mine(today, today)
        .then(({ data }) => setShifts(data))
        .catch(() => setShifts([]));
      profileApi
        .faceStatus()
        .then(({ data }) => setFaceRegistered(data.registered))
        .catch(() => setFaceRegistered(null));
    }, []),
  );

  return { shifts, faceRegistered };
}

/** Đồng hồ cập nhật mỗi 15 giây (đủ để hiển thị giờ:phút). */
function useClock(): Date {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const timer = setInterval(() => setNow(new Date()), 15_000);
    return () => clearInterval(timer);
  }, []);
  return now;
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.background },
  content: { padding: 16, paddingBottom: 32 },
  flex: { flex: 1 },
  header: { flexDirection: 'row', alignItems: 'center', gap: 12, marginBottom: 16 },
  headerText: { flex: 1 },
  greeting: { fontSize: 14, color: colors.muted },
  name: { fontSize: 20, fontWeight: '800', color: colors.foreground },
  clockCard: { backgroundColor: colors.header, borderRadius: radius.lg, padding: 20, alignItems: 'center' },
  date: { fontSize: 14, color: 'rgba(255,255,255,0.7)' },
  clock: { marginTop: 4, fontSize: 48, fontWeight: '800', color: colors.white, letterSpacing: 1 },
  checkIn: {
    marginTop: 16,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 8,
    backgroundColor: colors.white,
    paddingHorizontal: 28,
    paddingVertical: 12,
    borderRadius: radius.full,
  },
  checkInText: { fontSize: 16, fontWeight: '700', color: colors.primary },
  clockHint: { marginTop: 12, fontSize: 12, color: 'rgba(255,255,255,0.6)' },
  section: { marginTop: 24, marginBottom: 10, marginLeft: 4, fontSize: 13, fontWeight: '700', color: colors.muted, textTransform: 'uppercase' },
  shiftCard: { flexDirection: 'row', gap: 12, alignItems: 'center' },
  shiftSpacing: { marginBottom: 8 },
  shiftIconActive: { backgroundColor: colors.primarySoft },
  cancelled: { textDecorationLine: 'line-through', color: colors.muted },
  faceReminder: {
    marginTop: 16,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    padding: 14,
    borderRadius: radius.lg,
    backgroundColor: colors.warningSoft,
    borderWidth: 1,
    borderColor: colors.warning,
  },
  faceTitle: { fontSize: 15, fontWeight: '700', color: colors.warning },
  shiftIcon: { width: 44, height: 44, borderRadius: 22, backgroundColor: colors.background, alignItems: 'center', justifyContent: 'center' },
  shiftTitle: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  shiftText: { marginTop: 2, fontSize: 13, lineHeight: 18, color: colors.muted },
  grid: { flexDirection: 'row', flexWrap: 'wrap', gap: 12 },
  tile: {
    flexBasis: '47%',
    flexGrow: 1,
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: 16,
    gap: 10,
  },
  tileIcon: { width: 40, height: 40, borderRadius: 12, alignItems: 'center', justifyContent: 'center' },
  tileLabel: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  pressed: { opacity: 0.85 },
});
