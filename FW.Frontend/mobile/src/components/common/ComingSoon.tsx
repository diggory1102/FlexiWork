import type { ComponentProps } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { Screen } from '@/components/ui/Screen';
import { colors } from '@/theme';

interface ComingSoonProps {
  icon: ComponentProps<typeof Ionicons>['name'];
  title: string;
  description: string;
  /** Cấp bậc trong kế hoạch (docs/PROGRESS.md) sẽ hoàn thiện chức năng này. */
  milestone: string;
}

/** Màn hình giữ chỗ cho chức năng thuộc các cấp bậc sau. */
export function ComingSoon({ icon, title, description, milestone }: ComingSoonProps) {
  return (
    <Screen edges={[]} contentStyle={styles.center}>
      <View style={styles.iconWrap}>
        <Ionicons name={icon} size={36} color={colors.primary} />
      </View>
      <Text style={styles.title}>{title}</Text>
      <Text style={styles.description}>{description}</Text>
      <View style={styles.pill}>
        <Ionicons name="construct-outline" size={14} color={colors.warning} />
        <Text style={styles.pillText}>Đang phát triển · {milestone}</Text>
      </View>
    </Screen>
  );
}

const styles = StyleSheet.create({
  center: { alignItems: 'center', justifyContent: 'center', paddingHorizontal: 32 },
  iconWrap: {
    width: 80,
    height: 80,
    borderRadius: 40,
    backgroundColor: colors.primarySoft,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: 20,
  },
  title: { fontSize: 18, fontWeight: '700', color: colors.foreground, textAlign: 'center' },
  description: { marginTop: 8, fontSize: 14, lineHeight: 21, color: colors.muted, textAlign: 'center' },
  pill: {
    marginTop: 20,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
    paddingHorizontal: 12,
    paddingVertical: 6,
    borderRadius: 999,
    backgroundColor: colors.warningSoft,
  },
  pillText: { fontSize: 12, fontWeight: '600', color: colors.warning },
});
