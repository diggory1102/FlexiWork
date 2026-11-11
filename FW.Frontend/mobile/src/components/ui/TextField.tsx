import { forwardRef, useState, type ComponentProps } from 'react';
import { Pressable, StyleSheet, Text, TextInput, View, type TextInputInstance, type TextInputProps } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { colors, radius } from '@/theme';

interface TextFieldProps extends TextInputProps {
  label?: string;
  hint?: string;
  error?: string | null;
  icon?: ComponentProps<typeof Ionicons>['name'];
  /** Ô mật khẩu: ẩn ký tự và có nút hiện/ẩn. */
  password?: boolean;
}

/** Ô nhập liệu có nhãn, biểu tượng, gợi ý và thông báo lỗi. */
export const TextField = forwardRef<TextInputInstance, TextFieldProps>(function TextField(
  { label, hint, error, icon, password, style, editable = true, ...props },
  ref,
) {
  const [focused, setFocused] = useState(false);
  const [visible, setVisible] = useState(false);

  return (
    <View style={styles.wrapper}>
      {label && <Text style={styles.label}>{label}</Text>}
      <View
        style={[
          styles.box,
          focused && styles.focused,
          !!error && styles.invalid,
          !editable && styles.disabled,
        ]}
      >
        {icon && <Ionicons name={icon} size={18} color={colors.muted} />}
        <TextInput
          ref={ref}
          {...props}
          editable={editable}
          secureTextEntry={password && !visible}
          placeholderTextColor="#94A3B8"
          onFocus={(e) => {
            setFocused(true);
            props.onFocus?.(e);
          }}
          onBlur={(e) => {
            setFocused(false);
            props.onBlur?.(e);
          }}
          style={[styles.input, style]}
        />
        {password && (
          <Pressable
            onPress={() => setVisible((v) => !v)}
            hitSlop={10}
            accessibilityRole="button"
            accessibilityLabel={visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
          >
            <Ionicons name={visible ? 'eye-off-outline' : 'eye-outline'} size={20} color={colors.muted} />
          </Pressable>
        )}
      </View>
      {error ? <Text style={styles.error}>{error}</Text> : hint ? <Text style={styles.hint}>{hint}</Text> : null}
    </View>
  );
});

const styles = StyleSheet.create({
  wrapper: { gap: 6 },
  label: { fontSize: 13, fontWeight: '600', color: colors.foreground },
  box: {
    height: 48,
    flexDirection: 'row',
    alignItems: 'center',
    gap: 10,
    paddingHorizontal: 14,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
  },
  focused: { borderColor: colors.primary },
  invalid: { borderColor: colors.danger },
  disabled: { backgroundColor: colors.background },
  input: { flex: 1, height: '100%', fontSize: 15, color: colors.foreground },
  hint: { fontSize: 12, color: colors.muted },
  error: { fontSize: 12, color: colors.danger },
});
