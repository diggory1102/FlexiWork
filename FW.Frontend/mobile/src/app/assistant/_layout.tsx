import { Stack } from 'expo-router';
import { colors } from '@/theme';

/** Trợ lý AI: cuộc trò chuyện → lịch sử hội thoại. */
export default function AssistantLayout() {
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
      <Stack.Screen name="index" options={{ title: 'Trợ lý AI' }} />
      <Stack.Screen name="history" options={{ title: 'Lịch sử hội thoại' }} />
    </Stack>
  );
}
