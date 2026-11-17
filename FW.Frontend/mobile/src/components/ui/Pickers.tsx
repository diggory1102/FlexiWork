import { useMemo, useState, type ReactNode } from 'react';
import { Image, Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { addDays, todayIso, weekdayIndex } from '@/lib/dates';
import { formatDate } from '@/lib/format';
import { pickImage } from '@/lib/pickImage';
import { notify } from '@/lib/dialog';
import type { PickedImage } from '@/services/multipart';
import { colors, radius } from '@/theme';

// ---------------------------------------------------------------- Khung chung

/** Ô chọn (giống TextField) — chạm để mở hộp chọn. */
function FieldBox({ label, value, placeholder, icon, error, onPress }: {
  label: string;
  value: string | null;
  placeholder: string;
  icon: 'calendar-outline' | 'time-outline';
  error?: string | null;
  onPress: () => void;
}) {
  return (
    <View style={styles.wrapper}>
      <Text style={styles.label}>{label}</Text>
      <Pressable
        onPress={onPress}
        accessibilityRole="button"
        accessibilityLabel={`${label}: ${value ?? placeholder}`}
        style={({ pressed }) => [styles.box, !!error && styles.invalid, pressed && styles.pressed]}
      >
        <Ionicons name={icon} size={18} color={colors.muted} />
        <Text style={[styles.value, !value && styles.placeholder]}>{value ?? placeholder}</Text>
        <Ionicons name="chevron-down" size={16} color={colors.muted} />
      </Pressable>
      {error ? <Text style={styles.error}>{error}</Text> : null}
    </View>
  );
}

/** Hộp chọn trượt từ cạnh dưới. */
function Sheet({ visible, title, onClose, children }: { visible: boolean; title: string; onClose: () => void; children: ReactNode }) {
  const insets = useSafeAreaInsets();
  return (
    <Modal visible={visible} transparent animationType="slide" onRequestClose={onClose}>
      <Pressable style={styles.backdrop} onPress={onClose} accessibilityLabel="Đóng" />
      <View style={[styles.sheet, { paddingBottom: Math.max(insets.bottom, 16) }]}>
        <View style={styles.sheetHeader}>
          <Text style={styles.sheetTitle}>{title}</Text>
          <Pressable onPress={onClose} hitSlop={10} accessibilityRole="button" accessibilityLabel="Đóng">
            <Ionicons name="close" size={22} color={colors.muted} />
          </Pressable>
        </View>
        {children}
      </View>
    </Modal>
  );
}

// ---------------------------------------------------------------- Chọn ngày

const weekdayShort = ['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'];

/** Ô chọn ngày (yyyy-MM-dd) bằng lịch tháng, tuần bắt đầu Thứ 2; ngày ngoài [min, max] bị khóa. */
export function DateField({ label, value, onChange, min, max, error, placeholder = 'Chọn ngày' }: {
  label: string;
  value: string | null;
  onChange: (value: string) => void;
  min?: string;
  max?: string;
  error?: string | null;
  placeholder?: string;
}) {
  const [open, setOpen] = useState(false);
  const [month, setMonth] = useState(() => (value ?? min ?? todayIso()).slice(0, 7));

  const cells = useMemo(() => {
    const first = `${month}-01`;
    const start = addDays(first, -weekdayIndex(first));
    return Array.from({ length: 42 }, (_, i) => addDays(start, i));
  }, [month]);

  const shiftMonth = (delta: number) => {
    const [y, m] = month.split('-').map(Number);
    const d = new Date(Date.UTC(y, m - 1 + delta, 1));
    setMonth(`${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`);
  };
  const [y, m] = month.split('-');
  const today = todayIso();

  return (
    <>
      <FieldBox
        label={label}
        value={value ? formatDate(value) : null}
        placeholder={placeholder}
        icon="calendar-outline"
        error={error}
        onPress={() => {
          setMonth((value ?? min ?? today).slice(0, 7));
          setOpen(true);
        }}
      />
      <Sheet visible={open} title={label} onClose={() => setOpen(false)}>
        <View style={styles.monthRow}>
          <Pressable onPress={() => shiftMonth(-1)} hitSlop={10} accessibilityLabel="Tháng trước">
            <Ionicons name="chevron-back" size={22} color={colors.foreground} />
          </Pressable>
          <Text style={styles.monthText}>
            Tháng {Number(m)}, {y}
          </Text>
          <Pressable onPress={() => shiftMonth(1)} hitSlop={10} accessibilityLabel="Tháng sau">
            <Ionicons name="chevron-forward" size={22} color={colors.foreground} />
          </Pressable>
        </View>
        <View style={styles.grid}>
          {weekdayShort.map((w) => (
            <Text key={w} style={styles.weekday}>
              {w}
            </Text>
          ))}
          {cells.map((day) => {
            const outside = day.slice(0, 7) !== month;
            const disabled = (!!min && day < min) || (!!max && day > max);
            const selected = day === value;
            return (
              <Pressable
                key={day}
                disabled={disabled}
                onPress={() => {
                  onChange(day);
                  setOpen(false);
                }}
                accessibilityRole="button"
                accessibilityLabel={formatDate(day)}
                accessibilityState={{ disabled, selected }}
                style={styles.cell}
              >
                <View style={[styles.dayDot, selected && styles.daySelected, day === today && !selected && styles.dayToday]}>
                  <Text style={[styles.dayText, outside && styles.dayOutside, disabled && styles.dayDisabled, selected && styles.dayTextSelected]}>
                    {Number(day.slice(8, 10))}
                  </Text>
                </View>
              </Pressable>
            );
          })}
        </View>
      </Sheet>
    </>
  );
}

// ---------------------------------------------------------------- Chọn giờ

const STEP_MINUTES = 15;
const times = Array.from({ length: (24 * 60) / STEP_MINUTES }, (_, i) => {
  const minutes = i * STEP_MINUTES;
  return `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
});
const ROW_HEIGHT = 48;

/** Ô chọn giờ "HH:mm", bước 15 phút. */
export function TimeField({ label, value, onChange, error, placeholder = 'Chọn giờ' }: {
  label: string;
  value: string | null;
  onChange: (value: string) => void;
  error?: string | null;
  placeholder?: string;
}) {
  const [open, setOpen] = useState(false);
  const initialIndex = Math.max(0, times.indexOf(value ?? '17:00'));

  return (
    <>
      <FieldBox label={label} value={value} placeholder={placeholder} icon="time-outline" error={error} onPress={() => setOpen(true)} />
      <Sheet visible={open} title={label} onClose={() => setOpen(false)}>
        <ScrollView style={styles.timeList} contentOffset={{ x: 0, y: Math.max(0, initialIndex - 2) * ROW_HEIGHT }}>
          {times.map((t) => (
            <Pressable
              key={t}
              onPress={() => {
                onChange(t);
                setOpen(false);
              }}
              style={({ pressed }) => [styles.timeRow, t === value && styles.timeSelected, pressed && styles.pressed]}
              accessibilityRole="button"
              accessibilityState={{ selected: t === value }}
            >
              <Text style={[styles.timeText, t === value && styles.timeTextSelected]}>{t}</Text>
              {t === value && <Ionicons name="checkmark" size={18} color={colors.primary} />}
            </Pressable>
          ))}
        </ScrollView>
      </Sheet>
    </>
  );
}

// ---------------------------------------------------------------- Ảnh minh chứng

/** Ảnh minh chứng: chụp / chọn ảnh, xem trước, bỏ ảnh. */
export function PhotoField({ label, value, onChange, required, hint }: {
  label: string;
  value: PickedImage | null;
  onChange: (image: PickedImage | null) => void;
  required?: boolean;
  hint?: string;
}) {
  const choose = async (source: 'camera' | 'library') => {
    const result = await pickImage(source);
    if (!result) return;
    if (result.ok) onChange(result.image);
    else notify(result.message);
  };

  return (
    <View style={styles.wrapper}>
      <Text style={styles.label}>
        {label}
        {required ? <Text style={styles.required}> *</Text> : <Text style={styles.optional}> (không bắt buộc)</Text>}
      </Text>
      {value ? (
        <View style={styles.photoRow}>
          <Image source={{ uri: value.uri }} style={styles.photo} accessibilityLabel="Ảnh minh chứng đã chọn" />
          <Pressable onPress={() => onChange(null)} style={({ pressed }) => [styles.photoAction, pressed && styles.pressed]} accessibilityRole="button">
            <Ionicons name="trash-outline" size={18} color={colors.danger} />
            <Text style={[styles.photoActionText, { color: colors.danger }]}>Bỏ ảnh</Text>
          </Pressable>
        </View>
      ) : (
        <View style={styles.photoButtons}>
          <Pressable onPress={() => choose('camera')} style={({ pressed }) => [styles.photoButton, pressed && styles.pressed]} accessibilityRole="button">
            <Ionicons name="camera-outline" size={20} color={colors.primary} />
            <Text style={styles.photoActionText}>Chụp ảnh</Text>
          </Pressable>
          <Pressable onPress={() => choose('library')} style={({ pressed }) => [styles.photoButton, pressed && styles.pressed]} accessibilityRole="button">
            <Ionicons name="images-outline" size={20} color={colors.primary} />
            <Text style={styles.photoActionText}>Chọn từ thư viện</Text>
          </Pressable>
        </View>
      )}
      {hint ? <Text style={styles.hint}>{hint}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  wrapper: { gap: 6 },
  label: { fontSize: 13, fontWeight: '600', color: colors.foreground },
  required: { color: colors.danger },
  optional: { fontWeight: '400', color: colors.muted },
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
  invalid: { borderColor: colors.danger },
  value: { flex: 1, fontSize: 15, color: colors.foreground },
  placeholder: { color: '#94A3B8' },
  error: { fontSize: 12, color: colors.danger },
  hint: { fontSize: 12, color: colors.muted },
  pressed: { opacity: 0.8 },
  backdrop: { flex: 1, backgroundColor: 'rgba(15,23,42,0.4)' },
  sheet: { backgroundColor: colors.surface, borderTopLeftRadius: radius.lg, borderTopRightRadius: radius.lg, paddingHorizontal: 16, paddingTop: 12 },
  sheetHeader: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingVertical: 8 },
  sheetTitle: { fontSize: 16, fontWeight: '700', color: colors.foreground },
  monthRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingVertical: 10, paddingHorizontal: 4 },
  monthText: { fontSize: 15, fontWeight: '700', color: colors.foreground },
  grid: { flexDirection: 'row', flexWrap: 'wrap', paddingBottom: 8 },
  weekday: { width: `${100 / 7}%`, textAlign: 'center', fontSize: 12, fontWeight: '600', color: colors.muted, paddingVertical: 6 },
  cell: { width: `${100 / 7}%`, alignItems: 'center', paddingVertical: 3 },
  dayDot: { width: 40, height: 40, borderRadius: 20, alignItems: 'center', justifyContent: 'center' },
  daySelected: { backgroundColor: colors.primary },
  dayToday: { borderWidth: 1, borderColor: colors.primary },
  dayText: { fontSize: 15, color: colors.foreground },
  dayOutside: { color: '#CBD5E1' },
  dayDisabled: { color: '#CBD5E1', textDecorationLine: 'line-through' },
  dayTextSelected: { color: colors.white, fontWeight: '700' },
  timeList: { maxHeight: 360 },
  timeRow: { height: ROW_HEIGHT, flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: 12, borderRadius: radius.sm },
  timeSelected: { backgroundColor: colors.primarySoft },
  timeText: { fontSize: 16, color: colors.foreground },
  timeTextSelected: { color: colors.primary, fontWeight: '700' },
  photoButtons: { flexDirection: 'row', gap: 10 },
  photoButton: {
    flex: 1,
    height: 48,
    flexDirection: 'row',
    gap: 8,
    alignItems: 'center',
    justifyContent: 'center',
    borderRadius: radius.md,
    borderWidth: 1,
    borderStyle: 'dashed',
    borderColor: colors.primary,
    backgroundColor: colors.primarySoft,
  },
  photoRow: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  photo: { width: 88, height: 88, borderRadius: radius.md, backgroundColor: colors.background },
  photoAction: { flexDirection: 'row', alignItems: 'center', gap: 6, padding: 8 },
  photoActionText: { fontSize: 14, fontWeight: '600', color: colors.primary },
});
