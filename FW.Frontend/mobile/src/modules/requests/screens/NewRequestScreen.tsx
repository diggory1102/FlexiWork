import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Button } from '@/components/ui/Button';
import { Card, Notice } from '@/components/ui/Feedback';
import { TextField } from '@/components/ui/TextField';
import { DateField, PhotoField, TimeField } from '@/components/ui/Pickers';
import { requestApi } from '@/services/requestApi';
import { scheduleApi } from '@/services/scheduleApi';
import { attendanceApi } from '@/services/attendanceApi';
import { getErrorMessage } from '@/services/apiClient';
import type { PickedImage } from '@/services/multipart';
import { notify } from '@/lib/dialog';
import { addDays, formatDayMonth, todayIso } from '@/lib/dates';
import { formatDate, formatTime } from '@/lib/format';
import { matchesSearch } from '@/lib/search';
import { colors, radius } from '@/theme';
import type { AttendanceLogItem } from '@/types/attendance';
import type { CreateRequestInput, LeaveBalance, LeaveType, RequestCategory, SwapOptions } from '@/types/requests';
import type { Schedule } from '@/types/scheduling';
import { categoryInfo, days } from '../requestUi';

/** Giới hạn khớp RequestRules ở backend. */
const MAX_BACK_DAYS = 7;
const MAX_REASON = 1000;

const categoryOrder: RequestCategory[] = ['leave', 'shift_swap', 'overtime', 'explanation'];

/** Số lựa chọn đổi ca hiện mỗi đợt — chi nhánh đông người thì danh sách không đẩy ô lý do và nút gửi đi quá xa. */
const OPTIONS_PER_PAGE = 20;

const dayCount = (from: string, to: string) => Math.round((Date.parse(to) - Date.parse(from)) / 86_400_000) + 1;

/** Ca đã công bố, chưa chấm công và chưa tới giờ — đem đổi được. */
const isSwappable = (s: Schedule, now: Date) =>
  s.isPublished && s.status === 'scheduled' && new Date(`${s.workDate}T${s.startTime}:00`).getTime() > now.getTime();

/** Lượt chấm công cần giải trình: đi muộn, về sớm hoặc đang chờ duyệt. */
const needsExplanation = (l: AttendanceLogItem) => l.lateMinutes > 0 || l.earlyMinutes > 0 || l.status === 'pending_approval';

/** Tạo đơn (UC 5.1): chọn loại đơn → điền thông tin theo nhóm → lý do, ảnh minh chứng → gửi. */
export function NewRequestScreen() {
  const [types, setTypes] = useState<LeaveType[] | null>(null);
  const [type, setType] = useState<LeaveType | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  // Nghỉ
  const [startDate, setStartDate] = useState<string | null>(null);
  const [endDate, setEndDate] = useState<string | null>(null);
  const [balance, setBalance] = useState<LeaveBalance | null>(null);
  // OT
  const [otDate, setOtDate] = useState<string | null>(null);
  const [otStart, setOtStart] = useState<string | null>(null);
  const [otEnd, setOtEnd] = useState<string | null>(null);
  // Đổi ca
  const [myShifts, setMyShifts] = useState<Schedule[] | null>(null);
  const [sourceId, setSourceId] = useState<number | null>(null);
  const [options, setOptions] = useState<SwapOptions | null>(null);
  const [swapMode, setSwapMode] = useState<'swap' | 'cover'>('swap');
  const [targetScheduleId, setTargetScheduleId] = useState<number | null>(null);
  const [targetUserId, setTargetUserId] = useState<number | null>(null);
  // Giải trình
  const [logs, setLogs] = useState<AttendanceLogItem[] | null>(null);
  const [logId, setLogId] = useState<number | null>(null);
  // Chung
  const [reason, setReason] = useState('');
  const [image, setImage] = useState<PickedImage | null>(null);

  const today = todayIso();

  useEffect(() => {
    requestApi
      .types()
      .then(({ data }) => setTypes(data))
      .catch((err) => {
        setTypes([]);
        setError(getErrorMessage(err));
      });
  }, []);

  // Dữ liệu cần cho từng nhóm đơn
  useEffect(() => {
    if (!type) return;
    setError(null);
    if (type.category === 'leave' && type.deductsBalance) {
      requestApi.balance().then(({ data }) => setBalance(data)).catch(() => setBalance(null));
    }
    if (type.category === 'shift_swap' && myShifts === null) {
      const now = new Date();
      scheduleApi
        .mine(today, addDays(today, 30))
        .then(({ data }) => setMyShifts(data.filter((s) => isSwappable(s, now))))
        .catch((err) => {
          setMyShifts([]);
          setError(getErrorMessage(err));
        });
    }
    if (type.category === 'explanation' && logs === null) {
      attendanceApi
        .mine(addDays(today, -MAX_BACK_DAYS), today, 1, 50)
        .then(({ data }) => setLogs(data.items.filter(needsExplanation)))
        .catch((err) => {
          setLogs([]);
          setError(getErrorMessage(err));
        });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [type]);

  // Chọn ca muốn đổi → tải ca của đồng nghiệp / người làm thay
  useEffect(() => {
    setOptions(null);
    setTargetScheduleId(null);
    setTargetUserId(null);
    if (sourceId == null) return;
    requestApi
      .swapOptions(sourceId)
      .then(({ data }) => setOptions(data))
      .catch((err) => setError(getErrorMessage(err)));
  }, [sourceId]);

  const grouped = useMemo(
    () => categoryOrder.map((c) => ({ category: c, items: (types ?? []).filter((t) => t.category === c) })).filter((g) => g.items.length > 0),
    [types],
  );

  const otOvernight = !!otStart && !!otEnd && otEnd <= otStart;

  const buildInput = (): CreateRequestInput | string => {
    if (!type) return 'Vui lòng chọn loại đơn.';
    const base = { leaveTypeId: type.id, reason: reason.trim() };
    if (!base.reason) return 'Vui lòng nhập lý do.';
    if (type.requiresAttachment && !image) return `Loại đơn "${type.name}" cần kèm ảnh minh chứng.`;
    switch (type.category) {
      case 'leave':
        if (!startDate || !endDate) return 'Vui lòng chọn ngày bắt đầu và ngày kết thúc nghỉ.';
        if (endDate < startDate) return 'Ngày kết thúc phải sau hoặc bằng ngày bắt đầu.';
        return { ...base, startDate, endDate };
      case 'overtime': {
        if (!otDate || !otStart || !otEnd) return 'Vui lòng chọn ngày, giờ bắt đầu và giờ kết thúc làm thêm.';
        if (otStart === otEnd) return 'Giờ kết thúc phải khác giờ bắt đầu.';
        const endDay = otOvernight ? addDays(otDate, 1) : otDate;
        return { ...base, startAt: `${otDate}T${otStart}:00`, endAt: `${endDay}T${otEnd}:00` };
      }
      case 'shift_swap':
        if (sourceId == null) return 'Vui lòng chọn ca của bạn muốn đổi.';
        if (swapMode === 'swap' && targetScheduleId == null) return 'Vui lòng chọn ca của đồng nghiệp muốn đổi.';
        if (swapMode === 'cover' && targetUserId == null) return 'Vui lòng chọn đồng nghiệp làm thay.';
        return swapMode === 'swap'
          ? { ...base, sourceScheduleId: sourceId, targetScheduleId: targetScheduleId! }
          : { ...base, sourceScheduleId: sourceId, targetUserId: targetUserId! };
      case 'explanation':
        if (logId == null) return 'Vui lòng chọn lượt chấm công cần giải trình.';
        return { ...base, attendanceLogId: logId };
    }
  };

  const submit = async () => {
    const input = buildInput();
    if (typeof input === 'string') return setError(input);
    setSaving(true);
    setError(null);
    try {
      const { data, message } = await requestApi.create(input, image);
      notify(message, 'Đã gửi đơn');
      router.replace({ pathname: '/request/[id]', params: { id: String(data.id) } });
    } catch (err) {
      setError(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  // ---------------------------------------------------------------- Bước 1: chọn loại đơn
  if (!type) {
    return (
      <ScrollView contentContainerStyle={styles.content}>
        <Text style={styles.lead}>Bạn muốn gửi đơn gì?</Text>
        <Notice tone="danger" message={error} />
        {types === null && <ActivityIndicator color={colors.primary} />}
        {grouped.map((g) => {
          const info = categoryInfo[g.category];
          return (
            <View key={g.category} style={styles.group}>
              <Text style={styles.section}>{info.label}</Text>
              {g.items.map((t) => (
                <Pressable key={t.id} onPress={() => setType(t)} style={({ pressed }) => [pressed && styles.pressed]} accessibilityRole="button">
                  <Card style={styles.typeCard}>
                    <View style={[styles.icon, { backgroundColor: `${info.tint}1A` }]}>
                      <Ionicons name={info.icon} size={20} color={info.tint} />
                    </View>
                    <View style={styles.flex}>
                      <Text style={styles.title}>{t.name}</Text>
                      <Text style={styles.muted}>{t.description ?? info.hint}</Text>
                    </View>
                    <Ionicons name="chevron-forward" size={18} color={colors.muted} />
                  </Card>
                </Pressable>
              ))}
            </View>
          );
        })}
      </ScrollView>
    );
  }

  // ---------------------------------------------------------------- Bước 2: điền đơn
  const info = categoryInfo[type.category];
  return (
    <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
      <Pressable onPress={() => setType(null)} style={({ pressed }) => [pressed && styles.pressed]} accessibilityRole="button" accessibilityLabel="Đổi loại đơn">
        <Card style={styles.typeCard}>
          <View style={[styles.icon, { backgroundColor: `${info.tint}1A` }]}>
            <Ionicons name={info.icon} size={20} color={info.tint} />
          </View>
          <View style={styles.flex}>
            <Text style={styles.title}>{type.name}</Text>
            <Text style={styles.muted}>{type.isPaid ? 'Có hưởng lương' : 'Không hưởng lương'}</Text>
          </View>
          <Text style={styles.link}>Đổi</Text>
        </Card>
      </Pressable>

      {type.category === 'leave' && (
        <>
          {balance && (
            <Notice
              tone="info"
              message={`Phép năm ${balance.year} còn xin được ${days(balance.availableDays)} ngày${
                balance.pendingDays > 0 ? ` (đã trừ ${days(balance.pendingDays)} ngày đang chờ duyệt)` : ''
              }.`}
            />
          )}
          <DateField
            label="Từ ngày"
            value={startDate}
            min={addDays(today, -MAX_BACK_DAYS)}
            onChange={(v) => {
              setStartDate(v);
              if (!endDate || endDate < v) setEndDate(v);
            }}
          />
          <DateField label="Đến ngày" value={endDate} min={startDate ?? addDays(today, -MAX_BACK_DAYS)} onChange={setEndDate} />
          {startDate && endDate && endDate >= startDate && (
            <Text style={styles.muted}>Tổng cộng {dayCount(startDate, endDate)} ngày (tính cả ngày đầu và ngày cuối).</Text>
          )}
        </>
      )}

      {type.category === 'overtime' && (
        <>
          <DateField label="Ngày làm thêm" value={otDate} min={addDays(today, -MAX_BACK_DAYS)} max={addDays(today, 60)} onChange={setOtDate} />
          <View style={styles.row}>
            <View style={styles.flex}>
              <TimeField label="Bắt đầu" value={otStart} onChange={setOtStart} />
            </View>
            <View style={styles.flex}>
              <TimeField label="Kết thúc" value={otEnd} onChange={setOtEnd} />
            </View>
          </View>
          {otOvernight && <Text style={styles.muted}>Kết thúc vào ngày hôm sau.</Text>}
          <Text style={styles.muted}>OT là giờ làm ngoài ca đã xếp, từ 30 phút đến 12 giờ.</Text>
        </>
      )}

      {type.category === 'shift_swap' && (
        <>
          <Text style={styles.label}>Ca của bạn muốn đổi</Text>
          {myShifts === null ? (
            <ActivityIndicator color={colors.primary} />
          ) : myShifts.length === 0 ? (
            <Notice tone="warning" message="Bạn không có ca nào sắp tới đã công bố để đổi (30 ngày tới)." />
          ) : (
            myShifts.map((s) => (
              <Option
                key={s.id}
                selected={sourceId === s.id}
                title={`${s.shiftName} · ${formatDayMonth(s.workDate)}`}
                subtitle={`${s.startTime}–${s.endTime}`}
                onPress={() => setSourceId(s.id)}
              />
            ))
          )}

          {sourceId != null && (
            <>
              <View style={styles.segment}>
                {(['swap', 'cover'] as const).map((m) => (
                  <Pressable
                    key={m}
                    onPress={() => setSwapMode(m)}
                    style={[styles.segmentItem, swapMode === m && styles.segmentActive]}
                    accessibilityRole="button"
                    accessibilityState={{ selected: swapMode === m }}
                  >
                    <Text style={[styles.segmentText, swapMode === m && styles.segmentTextActive]}>
                      {m === 'swap' ? 'Đổi với ca khác' : 'Nhờ làm thay'}
                    </Text>
                  </Pressable>
                ))}
              </View>
              {options === null ? (
                <ActivityIndicator color={colors.primary} />
              ) : swapMode === 'swap' ? (
                options.shifts.length === 0 ? (
                  <Notice tone="warning" message="Chưa có ca nào của đồng nghiệp trong 7 ngày quanh ca này để đổi. Bạn có thể nhờ làm thay." />
                ) : (
                  <SearchableOptions
                    items={options.shifts}
                    searchFields={(o) => [o.fullName, o.employeeCode]}
                    isSelected={(o) => targetScheduleId === o.scheduleId}
                    renderItem={(o) => (
                      <Option
                        key={o.scheduleId}
                        selected={targetScheduleId === o.scheduleId}
                        title={`${o.fullName} · ${o.shiftName}`}
                        subtitle={`${o.employeeCode} · ${formatDayMonth(o.workDate)} · ${o.startTime}–${o.endTime}`}
                        onPress={() => setTargetScheduleId(o.scheduleId)}
                      />
                    )}
                  />
                )
              ) : options.colleagues.length === 0 ? (
                <Notice tone="warning" message="Chi nhánh chưa có đồng nghiệp nào khác." />
              ) : (
                <SearchableOptions
                  items={options.colleagues}
                  searchFields={(c) => [c.fullName, c.employeeCode]}
                  isSelected={(c) => targetUserId === c.id}
                  renderItem={(c) => (
                    <Option
                      key={c.id}
                      selected={targetUserId === c.id}
                      title={c.fullName}
                      subtitle={c.employeeCode}
                      onPress={() => setTargetUserId(c.id)}
                    />
                  )}
                />
              )}
              <Text style={styles.muted}>Đồng nghiệp đồng ý trước, sau đó Quản lý duyệt.</Text>
            </>
          )}
        </>
      )}

      {type.category === 'explanation' && (
        <>
          <Text style={styles.label}>Lượt chấm công cần giải trình (7 ngày gần đây)</Text>
          {logs === null ? (
            <ActivityIndicator color={colors.primary} />
          ) : logs.length === 0 ? (
            <Notice tone="success" message="Bạn không có lượt chấm công nào đi muộn, về sớm hay chờ duyệt trong 7 ngày qua." />
          ) : (
            logs.map((l) => (
              <Option
                key={l.id}
                selected={logId === l.id}
                title={`${l.shiftName ?? 'Ca'} · ${formatDate(l.workDate)}`}
                subtitle={[
                  `Vào ${formatTime(l.checkinTime)}${l.checkoutTime ? ` · ra ${formatTime(l.checkoutTime)}` : ''}`,
                  l.lateMinutes > 0 ? `muộn ${l.lateMinutes} phút` : null,
                  l.earlyMinutes > 0 ? `sớm ${l.earlyMinutes} phút` : null,
                  l.status === 'pending_approval' ? 'chờ duyệt' : null,
                ]
                  .filter(Boolean)
                  .join(' · ')}
                onPress={() => setLogId(l.id)}
              />
            ))
          )}
        </>
      )}

      <TextField
        label="Lý do"
        value={reason}
        onChangeText={setReason}
        placeholder="Trình bày ngắn gọn lý do…"
        multiline
        maxLength={MAX_REASON}
        style={styles.textarea}
        hint={`${reason.length}/${MAX_REASON} ký tự`}
      />
      <PhotoField
        label="Ảnh minh chứng"
        value={image}
        onChange={setImage}
        required={type.requiresAttachment}
        hint={type.requiresAttachment ? 'VD: giấy khám bệnh, đơn thuốc.' : undefined}
      />

      <Notice tone="danger" message={error} />
      <Button title="Gửi đơn" loading={saving} onPress={submit} icon={<Ionicons name="send" size={18} color={colors.onPrimary} />} />
    </ScrollView>
  );
}

/**
 * Danh sách lựa chọn có ô tìm theo tên / mã nhân viên, hiện dần từng đợt. Mục đang chọn luôn nhìn thấy
 * (đưa lên đầu nếu không khớp từ khóa hoặc nằm ngoài đợt đang hiện).
 */
function SearchableOptions<T>({
  items,
  searchFields,
  isSelected,
  renderItem,
}: {
  items: T[];
  searchFields: (item: T) => string[];
  isSelected: (item: T) => boolean;
  renderItem: (item: T) => ReactNode;
}) {
  const [query, setQuery] = useState('');
  const [limit, setLimit] = useState(OPTIONS_PER_PAGE);

  const matched = items.filter((item) => matchesSearch(query, ...searchFields(item)));
  const shown = matched.slice(0, limit);
  const selected = items.find(isSelected);
  if (selected && !shown.includes(selected)) shown.unshift(selected);

  return (
    <>
      <TextField
        icon="search-outline"
        placeholder="Tìm theo tên hoặc mã nhân viên"
        accessibilityLabel="Tìm theo tên hoặc mã nhân viên"
        value={query}
        onChangeText={(text) => {
          setQuery(text);
          setLimit(OPTIONS_PER_PAGE);
        }}
        autoCapitalize="none"
        autoCorrect={false}
        testID="swap-search"
      />
      {matched.length === 0 && <Text style={styles.muted}>Không tìm thấy đồng nghiệp phù hợp.</Text>}
      {shown.map(renderItem)}
      {matched.length > limit && (
        <Button variant="ghost" title={`Xem thêm (còn ${matched.length - limit})`} onPress={() => setLimit(limit + OPTIONS_PER_PAGE)} />
      )}
    </>
  );
}

/** Một lựa chọn dạng thẻ có dấu tích. */
function Option({ selected, title, subtitle, onPress }: { selected: boolean; title: string; subtitle?: ReactNode; onPress: () => void }) {
  return (
    <Pressable
      onPress={onPress}
      style={({ pressed }) => [styles.option, selected && styles.optionSelected, pressed && styles.pressed]}
      accessibilityRole="radio"
      accessibilityState={{ selected }}
    >
      <Ionicons name={selected ? 'radio-button-on' : 'radio-button-off'} size={20} color={selected ? colors.primary : colors.muted} />
      <View style={styles.flex}>
        <Text style={styles.optionTitle}>{title}</Text>
        {subtitle ? <Text style={styles.muted}>{subtitle}</Text> : null}
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 16, paddingBottom: 40, gap: 12 },
  lead: { fontSize: 17, fontWeight: '700', color: colors.foreground },
  group: { gap: 8 },
  section: { marginTop: 4, marginLeft: 4, fontSize: 13, fontWeight: '700', color: colors.muted, textTransform: 'uppercase' },
  typeCard: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  icon: { width: 40, height: 40, borderRadius: 12, alignItems: 'center', justifyContent: 'center' },
  title: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  muted: { fontSize: 13, lineHeight: 18, color: colors.muted },
  link: { fontSize: 14, fontWeight: '600', color: colors.primary },
  label: { fontSize: 13, fontWeight: '600', color: colors.foreground },
  row: { flexDirection: 'row', gap: 12 },
  textarea: { minHeight: 100, paddingTop: 8, textAlignVertical: 'top' },
  option: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 12,
    padding: 14,
    borderRadius: radius.md,
    borderWidth: 1,
    borderColor: colors.border,
    backgroundColor: colors.surface,
  },
  optionSelected: { borderColor: colors.primary, backgroundColor: colors.primarySoft },
  optionTitle: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  segment: { flexDirection: 'row', backgroundColor: colors.border, borderRadius: radius.md, padding: 3 },
  segmentItem: { flex: 1, paddingVertical: 10, borderRadius: radius.sm, alignItems: 'center' },
  segmentActive: { backgroundColor: colors.surface },
  segmentText: { fontSize: 14, fontWeight: '600', color: colors.muted },
  segmentTextActive: { color: colors.primary },
  pressed: { opacity: 0.85 },
});
