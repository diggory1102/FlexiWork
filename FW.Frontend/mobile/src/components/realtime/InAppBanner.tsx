import { useEffect } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useBannerStore } from '@/stores/bannerStore';
import { colors, radius } from '@/theme';

const VISIBLE_MS = 4500;

/** Banner nổi ở đầu màn hình khi có thông báo / tin nhắn mới lúc app đang mở; chạm để mở, tự ẩn sau vài giây. */
export function InAppBanner() {
  const banner = useBannerStore((s) => s.current);
  const hide = useBannerStore((s) => s.hide);
  const insets = useSafeAreaInsets();

  useEffect(() => {
    if (!banner) return;
    const timer = setTimeout(hide, VISIBLE_MS);
    return () => clearTimeout(timer);
  }, [banner, hide]);

  if (!banner) return null;

  const open = () => {
    hide();
    if (banner.href) router.push(banner.href);
  };

  return (
    <View pointerEvents="box-none" style={[styles.wrap, { top: insets.top + 8 }]}>
      <Pressable testID="in-app-banner" onPress={open} accessibilityRole="button" style={styles.card}>
        <View style={styles.icon}>
          <Ionicons name="notifications" size={18} color={colors.primary} />
        </View>
        <View style={styles.texts}>
          <Text style={styles.title} numberOfLines={1}>
            {banner.title}
          </Text>
          {banner.body ? (
            <Text style={styles.body} numberOfLines={2}>
              {banner.body}
            </Text>
          ) : null}
        </View>
        <Pressable onPress={hide} hitSlop={10} accessibilityLabel="Đóng">
          <Ionicons name="close" size={18} color={colors.muted} />
        </Pressable>
      </Pressable>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { position: 'absolute', left: 12, right: 12, zIndex: 100 },
  card: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    backgroundColor: colors.surface,
    borderRadius: radius.lg,
    borderWidth: 1,
    borderColor: colors.border,
    padding: 12,
    shadowColor: '#000',
    shadowOpacity: 0.12,
    shadowRadius: 12,
    shadowOffset: { width: 0, height: 4 },
    elevation: 6,
  },
  icon: { width: 34, height: 34, borderRadius: 17, backgroundColor: colors.primarySoft, alignItems: 'center', justifyContent: 'center' },
  texts: { flex: 1, gap: 2 },
  title: { fontSize: 14, fontWeight: '700', color: colors.foreground },
  body: { fontSize: 13, color: colors.muted },
});
