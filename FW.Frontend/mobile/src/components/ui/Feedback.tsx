import type { ComponentProps, ReactNode } from 'react';
import { StyleSheet, Text, View, type StyleProp, type ViewStyle } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, radius } from '@/theme';

type Tone = 'info' | 'success' | 'warning' | 'danger';

const tones: Record<Tone, { bg: string; fg: string; icon: ComponentProps<typeof Ionicons>['name'] }> = {
  info: { bg: colors.primarySoft, fg: colors.primaryDark, icon: 'information-circle-outline' },
  success: { bg: colors.successSoft, fg: colors.success, icon: 'checkmark-circle-outline' },
  warning: { bg: colors.warningSoft, fg: colors.warning, icon: 'alert-circle-outline' },
  danger: { bg: colors.dangerSoft, fg: colors.danger, icon: 'close-circle-outline' },
};

/** Hộp thông báo trong form (lỗi, thông tin, thành công). Không hiển thị khi message rỗng. */
export function Notice({ tone = 'info', message, style }: { tone?: Tone; message?: string | null; style?: StyleProp<ViewStyle> }) {
  if (!message) return null;
  const t = tones[tone];
  return (
    <View style={[styles.notice, { backgroundColor: t.bg }, style]} accessibilityRole="alert">
      <Ionicons name={t.icon} size={18} color={t.fg} />
      <Text style={[styles.noticeText, { color: t.fg }]}>{message}</Text>
    </View>
  );
}

/** Thẻ nền trắng bo góc. */
export function Card({ children, style }: { children: ReactNode; style?: StyleProp<ViewStyle> }) {
  return <View style={[styles.card, style]}>{children}</View>;
}

/** Nhãn nhỏ (vai trò, trạng thái). */
export function Badge({ label, tone = 'info' }: { label: string; tone?: Tone }) {
  const t = tones[tone];
  return (
    <View style={[styles.badge, { backgroundColor: t.bg }]}>
      <Text style={[styles.badgeText, { color: t.fg }]}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  notice: { flexDirection: 'row', gap: 8, padding: 12, borderRadius: radius.md, alignItems: 'flex-start' },
  noticeText: { flex: 1, fontSize: 13, lineHeight: 19 },
  card: {
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: 16,
  },
  badge: { paddingHorizontal: 10, paddingVertical: 3, borderRadius: radius.full },
  badgeText: { fontSize: 12, fontWeight: '600' },
});
