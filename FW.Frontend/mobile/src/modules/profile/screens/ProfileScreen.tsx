import { useEffect, useState, type ComponentProps } from 'react';
import { ActivityIndicator, Alert, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Avatar } from '@/components/ui/Avatar';
import { Badge, Card, Notice } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { Button } from '@/components/ui/Button';
import { useAuthStore } from '@/stores/authStore';
import { confirmAction, notify } from '@/lib/dialog';
import { employmentTypeLabels, formatDate, roleLabels, userStatusLabels } from '@/lib/format';
import { colors, radius } from '@/theme';
import { useProfile, type AvatarSource } from '../hooks/useProfile';

/** Hồ sơ cá nhân: xem thông tin, đổi ảnh đại diện, sửa số điện thoại, đổi mật khẩu, đăng xuất. */
export function ProfileScreen() {
  const logout = useAuthStore((s) => s.logout);
  const vm = useProfile();
  const [phone, setPhone] = useState('');
  const [phoneMessage, setPhoneMessage] = useState<{ ok: boolean; message: string } | null>(null);

  useEffect(() => {
    if (vm.profile) setPhone(vm.profile.phoneNumber ?? '');
  }, [vm.profile]);

  const pickAvatar = () => {
    const run = async (source: AvatarSource) => {
      const result = await vm.changeAvatar(source);
      if (result) notify(result.message, result.ok ? 'Thành công' : 'Không thể đổi ảnh');
    };
    // Bản web không có camera trong image picker → chọn từ máy
    if (Platform.OS === 'web') return void run('library');
    Alert.alert('Ảnh đại diện', 'Chọn nguồn ảnh', [
      { text: 'Chụp ảnh', onPress: () => void run('camera') },
      { text: 'Chọn từ thư viện', onPress: () => void run('library') },
      { text: 'Hủy', style: 'cancel' },
    ]);
  };

  const savePhone = async () => {
    setPhoneMessage(await vm.savePhone(phone));
  };

  if (vm.loading) {
    return (
      <View style={styles.center}>
        <ActivityIndicator color={colors.primary} />
      </View>
    );
  }

  const p = vm.profile;
  if (!p) {
    return (
      <View style={[styles.center, styles.padded]}>
        <Notice tone="danger" message={vm.error ?? 'Không tải được hồ sơ.'} />
        <Button title="Thử lại" variant="secondary" onPress={vm.refresh} style={styles.retry} />
      </View>
    );
  }

  const phoneChanged = phone.trim() !== (p.phoneNumber ?? '');
  const info: [ComponentProps<typeof Ionicons>['name'], string, string][] = [
    ['id-card-outline', 'Mã nhân viên', p.employeeCode],
    ['mail-outline', 'Email', p.email],
    ['storefront-outline', 'Chi nhánh', p.branchName ?? '—'],
    ['people-outline', 'Phòng ban', p.departmentName ?? '—'],
    ['briefcase-outline', 'Chức vụ', p.positionTitle ?? '—'],
    ['document-text-outline', 'Loại hợp đồng', employmentTypeLabels[p.employmentType]],
    ['calendar-outline', 'Ngày vào làm', formatDate(p.hireDate)],
  ];

  return (
    <ScrollView
      style={styles.screen}
      contentContainerStyle={styles.content}
      keyboardShouldPersistTaps="handled"
      refreshControl={<RefreshControl refreshing={vm.refreshing} onRefresh={vm.refresh} tintColor={colors.primary} />}
    >
      <Card style={styles.hero}>
        <Pressable onPress={pickAvatar} disabled={vm.uploading} accessibilityRole="button" accessibilityLabel="Đổi ảnh đại diện">
          <Avatar name={p.fullName} url={p.avatarUrl} size={96} />
          <View style={styles.cameraBadge}>
            {vm.uploading ? <ActivityIndicator size="small" color={colors.white} /> : <Ionicons name="camera" size={16} color={colors.white} />}
          </View>
        </Pressable>
        <Text style={styles.name}>{p.fullName}</Text>
        <View style={styles.badges}>
          {p.roles.map((r) => (
            <Badge key={r} label={roleLabels[r]} />
          ))}
          <Badge label={userStatusLabels[p.status]} tone={p.status === 'active' ? 'success' : 'warning'} />
        </View>
      </Card>

      <Text style={styles.section}>Thông tin công việc</Text>
      <Card style={styles.list}>
        {info.map(([icon, label, value], index) => (
          <View key={label} style={[styles.row, index > 0 && styles.divider]}>
            <Ionicons name={icon} size={18} color={colors.muted} />
            <Text style={styles.rowLabel}>{label}</Text>
            <Text style={styles.rowValue} numberOfLines={1}>
              {value}
            </Text>
          </View>
        ))}
      </Card>
      <Text style={styles.note}>Thông tin công việc do bộ phận Nhân sự quản lý. Liên hệ HR nếu cần điều chỉnh.</Text>

      <Text style={styles.section}>Liên hệ</Text>
      <Card style={styles.phoneCard}>
        <TextField
          label="Số điện thoại"
          icon="call-outline"
          placeholder="VD: 0912345678"
          keyboardType="phone-pad"
          maxLength={15}
          value={phone}
          onChangeText={(v) => {
            setPhone(v);
            setPhoneMessage(null);
          }}
        />
        <Notice tone={phoneMessage?.ok ? 'success' : 'danger'} message={phoneMessage?.message} />
        <Button title="Lưu số điện thoại" onPress={savePhone} loading={vm.savingPhone} disabled={!phoneChanged} />
      </Card>

      <Text style={styles.section}>Tài khoản</Text>
      <Card style={styles.list}>
        <MenuRow icon="key-outline" label="Đổi mật khẩu" onPress={() => router.push('/change-password')} />
        <MenuRow
          icon="log-out-outline"
          label="Đăng xuất"
          danger
          divider
          onPress={() => confirmAction('Đăng xuất', 'Bạn muốn đăng xuất khỏi FlexiWork trên thiết bị này?', 'Đăng xuất', logout, true)}
        />
      </Card>
    </ScrollView>
  );
}

function MenuRow({
  icon,
  label,
  onPress,
  danger,
  divider,
}: {
  icon: ComponentProps<typeof Ionicons>['name'];
  label: string;
  onPress: () => void;
  danger?: boolean;
  divider?: boolean;
}) {
  const color = danger ? colors.danger : colors.foreground;
  return (
    <Pressable onPress={onPress} style={({ pressed }) => [styles.row, divider && styles.divider, pressed && styles.pressed]} accessibilityRole="button">
      <Ionicons name={icon} size={18} color={danger ? colors.danger : colors.muted} />
      <Text style={[styles.menuLabel, { color }]}>{label}</Text>
      {!danger && <Ionicons name="chevron-forward" size={18} color={colors.muted} />}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1, backgroundColor: colors.background },
  content: { padding: 16, paddingBottom: 32 },
  center: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.background },
  padded: { padding: 24 },
  retry: { marginTop: 16, alignSelf: 'stretch' },
  hero: { alignItems: 'center', paddingVertical: 24 },
  cameraBadge: {
    position: 'absolute',
    right: 0,
    bottom: 0,
    width: 32,
    height: 32,
    borderRadius: 16,
    backgroundColor: colors.primary,
    borderWidth: 2,
    borderColor: colors.white,
    alignItems: 'center',
    justifyContent: 'center',
  },
  name: { marginTop: 12, fontSize: 20, fontWeight: '700', color: colors.foreground, textAlign: 'center' },
  badges: { marginTop: 8, flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'center', gap: 6 },
  section: { marginTop: 20, marginBottom: 8, marginLeft: 4, fontSize: 13, fontWeight: '700', color: colors.muted, textTransform: 'uppercase' },
  list: { paddingVertical: 0, paddingHorizontal: 0, overflow: 'hidden' },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12, paddingHorizontal: 16, paddingVertical: 14 },
  divider: { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border },
  pressed: { backgroundColor: colors.background },
  rowLabel: { fontSize: 14, color: colors.muted },
  rowValue: { flex: 1, fontSize: 14, fontWeight: '500', color: colors.foreground, textAlign: 'right' },
  note: { marginTop: 8, marginHorizontal: 4, fontSize: 12, lineHeight: 17, color: colors.muted },
  phoneCard: { gap: 12 },
  menuLabel: { flex: 1, fontSize: 15, fontWeight: '500' },
});
