import { Stack } from 'expo-router';
import { colors } from '@/theme';

/** Phiếu lương: danh sách → chi tiết, dùng chung header với ngăn xếp gốc. */
export default function PayslipsLayout() {
  return (
    <Stack
      screenOptions={{
        headerTitleAlign: 'center',
        headerTintColor: colors.foreground,
        headerStyle: { backgroundColor: colors.surface },
        headerTitleStyle: { fontWeight: '700' },
        headerBackButtonDisplayMode: 'minimal',
        contentStyle: { backgroundColor: colors.background },
      }}
    >
      <Stack.Screen name="index" options={{ title: 'Phiếu lương' }} />
      <Stack.Screen name="[id]" options={{ title: 'Chi tiết phiếu lương' }} />
    </Stack>
  );
}
