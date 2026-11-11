import { StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '@/theme';

/** Logo FlexiWork (đồng hồ + dấu tích chấm công) — cùng nhận diện với Web Admin. */
export function Logo({ size = 56, showText = true }: { size?: number; showText?: boolean }) {
  const badge = size * 0.38;
  return (
    <View style={styles.row}>
      <View style={[styles.mark, { width: size, height: size, borderRadius: size * 0.25 }]}>
        <Ionicons name="time-outline" size={size * 0.62} color={colors.white} />
        <View style={[styles.badge, { width: badge, height: badge, borderRadius: badge / 2, right: -badge * 0.2, bottom: -badge * 0.2 }]}>
          <Ionicons name="checkmark" size={badge * 0.7} color={colors.white} />
        </View>
      </View>
      {showText && (
        <View>
          <Text style={styles.title}>FlexiWork</Text>
          <Text style={styles.subtitle}>Quản lý ca & chấm công</Text>
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  row: { flexDirection: 'row', alignItems: 'center', gap: 14 },
  mark: { backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center' },
  badge: {
    position: 'absolute',
    backgroundColor: '#22C55E',
    borderWidth: 2,
    borderColor: colors.white,
    alignItems: 'center',
    justifyContent: 'center',
  },
  title: { fontSize: 24, fontWeight: '800', color: colors.foreground, letterSpacing: -0.5 },
  subtitle: { fontSize: 13, color: colors.muted },
});
