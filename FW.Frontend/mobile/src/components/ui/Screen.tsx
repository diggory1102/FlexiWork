import type { ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, View, type StyleProp, type ViewStyle } from 'react-native';
import { SafeAreaView, type Edge } from 'react-native-safe-area-context';
import { colors } from '@/theme';

interface ScreenProps {
  children: ReactNode;
  /** Cuộn được (mặc định true) — form dài không bị bàn phím che. */
  scroll?: boolean;
  /** Cạnh cần chừa vùng an toàn; màn hình có header/tab bar chỉ cần một phần. */
  edges?: Edge[];
  background?: string;
  contentStyle?: StyleProp<ViewStyle>;
}

/** Khung màn hình chuẩn: vùng an toàn + tránh bàn phím + cuộn. */
export function Screen({ children, scroll = true, edges = ['top', 'bottom'], background = colors.background, contentStyle }: ScreenProps) {
  return (
    <SafeAreaView edges={edges} style={[styles.flex, { backgroundColor: background }]}>
      <KeyboardAvoidingView style={styles.flex} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        {scroll ? (
          <ScrollView
            style={styles.flex}
            contentContainerStyle={[styles.content, contentStyle]}
            keyboardShouldPersistTaps="handled"
            showsVerticalScrollIndicator={false}
          >
            {children}
          </ScrollView>
        ) : (
          <View style={[styles.flex, styles.content, contentStyle]}>{children}</View>
        )}
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { flexGrow: 1, padding: 20 },
});
