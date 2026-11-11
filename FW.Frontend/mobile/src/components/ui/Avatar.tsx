import { useEffect, useState } from 'react';
import { Image, StyleSheet, Text, View } from 'react-native';
import { initials } from '@/lib/format';
import { colors } from '@/theme';

/** Ảnh đại diện; nếu chưa có ảnh hoặc ảnh lỗi thì hiển thị chữ cái đầu của họ tên. */
export function Avatar({ name, url, size = 40 }: { name: string; url?: string | null; size?: number }) {
  const [broken, setBroken] = useState(false);
  useEffect(() => setBroken(false), [url]);
  const shape = { width: size, height: size, borderRadius: size / 2 };

  if (url && !broken) {
    return <Image source={{ uri: url }} style={[shape, styles.image]} onError={() => setBroken(true)} accessibilityIgnoresInvertColors />;
  }
  return (
    <View style={[shape, styles.fallback]}>
      <Text style={[styles.text, { fontSize: size * 0.36 }]}>{initials(name)}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  image: { backgroundColor: colors.border },
  fallback: { backgroundColor: colors.primarySoft, alignItems: 'center', justifyContent: 'center' },
  text: { color: colors.primary, fontWeight: '700' },
});
