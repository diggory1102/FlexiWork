import { Stack } from 'expo-router';
import { colors } from '@/theme';

/** Chat: danh sách phòng → cuộc trò chuyện → chọn người / thông tin nhóm. */
export default function ChatLayout() {
  return (
    <Stack
      screenOptions={{
        headerTitleAlign: 'center',
        headerTintColor: colors.foreground,
        headerStyle: { backgroundColor: colors.chrome },
        headerTitleStyle: { fontWeight: '700' },
        headerBackButtonDisplayMode: 'minimal',
        contentStyle: { backgroundColor: colors.background },
      }}
    >
      <Stack.Screen name="index" options={{ title: 'Tin nhắn' }} />
      <Stack.Screen name="[id]" options={{ title: 'Cuộc trò chuyện' }} />
      <Stack.Screen name="new" options={{ title: 'Cuộc trò chuyện mới' }} />
      <Stack.Screen name="info/[id]" options={{ title: 'Thông tin nhóm' }} />
    </Stack>
  );
}
