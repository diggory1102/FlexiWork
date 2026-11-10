import { Stack } from 'expo-router';
import { StatusBar } from 'expo-status-bar';

/** Layout gốc của ứng dụng (Expo Router). Mỗi file trong src/app là một màn hình. */
export default function RootLayout() {
  return (
    <>
      <StatusBar style="dark" />
      <Stack screenOptions={{ headerTitleAlign: 'center' }}>
        <Stack.Screen name="index" options={{ title: 'FlexiWork' }} />
      </Stack>
    </>
  );
}
