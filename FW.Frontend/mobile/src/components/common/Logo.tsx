import { Image, StyleSheet, Text, View } from 'react-native';
import { colors } from '@/theme';

// Cùng ảnh với Web Admin (web/src/assets): chỉ lấy hình, màu tô bằng màu nhấn của theme
const icon = require('../../../assets/logo-icon.png');
const wordmark = require('../../../assets/logo-wordmark.png');

export const SLOGAN = 'Flex your schedule. Own your work.';

/** Logo FlexiWork xếp dọc: biểu tượng bàn làm việc, tên thương hiệu và khẩu hiệu. */
export function Logo({ width = 220 }: { width?: number }) {
  const iconWidth = Math.round(width * 0.6);
  return (
    <View style={styles.column}>
      <Image source={icon} tintColor={colors.primary} resizeMode="contain" style={{ width: iconWidth, height: Math.round(iconWidth * 0.877) }} />
      <Image
        source={wordmark}
        tintColor={colors.primary}
        resizeMode="contain"
        style={{ width, height: Math.round(width * 0.163) }}
        accessibilityLabel="FlexiWork"
      />
      <Text style={styles.slogan}>{SLOGAN}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  column: { alignItems: 'center', gap: 10 },
  slogan: { fontSize: 13, color: colors.muted },
});
