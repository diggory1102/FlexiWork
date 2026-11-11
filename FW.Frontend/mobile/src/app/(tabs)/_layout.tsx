import type { ComponentProps } from 'react';
import type { ColorValue } from 'react-native';
import { Tabs } from 'expo-router';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { colors } from '@/theme';

type IconName = ComponentProps<typeof Ionicons>['name'];

/** Biểu tượng tab: dạng đặc khi đang chọn, dạng viền khi không chọn. */
const tabIcon =
  (name: string) =>
  ({ color, size, focused }: { color: ColorValue; size: number; focused: boolean }) => (
    <Ionicons name={(focused ? name : `${name}-outline`) as IconName} size={size} color={typeof color === 'string' ? color : colors.muted} />
  );

/** Khung điều hướng tab chính của ứng dụng nhân viên. */
export default function TabsLayout() {
  // Chiều cao mặc định quá thấp với nhãn tiếng Việt có dấu → tự đặt, cộng thêm vùng an toàn cạnh dưới
  const insets = useSafeAreaInsets();
  const bottom = Math.max(insets.bottom, 8);

  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: colors.primary,
        tabBarInactiveTintColor: colors.muted,
        tabBarStyle: {
          backgroundColor: colors.surface,
          borderTopColor: colors.border,
          height: 64 + bottom,
          paddingTop: 4,
          paddingBottom: bottom,
        },
        tabBarLabelStyle: { fontSize: 11, lineHeight: 15, fontWeight: '600' },
        headerTitleAlign: 'center',
        headerStyle: { backgroundColor: colors.surface },
        headerTitleStyle: { fontWeight: '700', color: colors.foreground },
        sceneStyle: { backgroundColor: colors.background },
      }}
    >
      <Tabs.Screen name="index" options={{ title: 'Trang chủ', headerShown: false, tabBarIcon: tabIcon('home') }} />
      <Tabs.Screen name="schedule" options={{ title: 'Lịch ca', tabBarIcon: tabIcon('calendar') }} />
      <Tabs.Screen name="attendance" options={{ title: 'Chấm công', tabBarIcon: tabIcon('finger-print') }} />
      <Tabs.Screen name="requests" options={{ title: 'Đơn từ', tabBarIcon: tabIcon('document-text') }} />
      <Tabs.Screen name="profile" options={{ title: 'Cá nhân', headerTitle: 'Hồ sơ cá nhân', tabBarIcon: tabIcon('person') }} />
    </Tabs>
  );
}
