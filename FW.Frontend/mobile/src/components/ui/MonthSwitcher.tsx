import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, radius } from '@/theme';

export interface YearMonth {
  year: number;
  month: number;
}

export function shiftMonth({ year, month }: YearMonth, delta: number): YearMonth {
  const index = year * 12 + (month - 1) + delta;
  return { year: Math.floor(index / 12), month: (index % 12) + 1 };
}

export const sameMonth = (a: YearMonth, b: YearMonth) => a.year === b.year && a.month === b.month;

/** Thanh chuyển tháng ‹ Tháng 11/2026 ›; không cho đi quá `max` (mặc định không giới hạn). */
export function MonthSwitcher({ value, onChange, max }: { value: YearMonth; onChange: (v: YearMonth) => void; max?: YearMonth }) {
  const atMax = !!max && sameMonth(value, max);
  return (
    <View style={styles.bar}>
      <Pressable testID="month-prev" onPress={() => onChange(shiftMonth(value, -1))} hitSlop={10} accessibilityLabel="Tháng trước" style={styles.button}>
        <Ionicons name="chevron-back" size={20} color={colors.foreground} />
      </Pressable>
      <Text style={styles.label}>
        Tháng {value.month}/{value.year}
      </Text>
      <Pressable
        testID="month-next"
        onPress={() => !atMax && onChange(shiftMonth(value, 1))}
        disabled={atMax}
        hitSlop={10}
        accessibilityLabel="Tháng sau"
        style={[styles.button, atMax && styles.disabled]}
      >
        <Ionicons name="chevron-forward" size={20} color={colors.foreground} />
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  bar: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    paddingHorizontal: 8,
    paddingVertical: 6,
  },
  button: { width: 40, height: 40, alignItems: 'center', justifyContent: 'center' },
  label: { fontSize: 16, fontWeight: '700', color: colors.foreground },
  disabled: { opacity: 0.3 },
});
