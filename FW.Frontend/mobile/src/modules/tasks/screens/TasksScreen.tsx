import { useCallback, useState } from 'react';
import { ActivityIndicator, Image, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import { router, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { Badge, Card, Notice } from '@/components/ui/Feedback';
import { Button } from '@/components/ui/Button';
import { TextField } from '@/components/ui/TextField';
import { PhotoField } from '@/components/ui/Pickers';
import { taskApi } from '@/services/taskApi';
import { getErrorMessage } from '@/services/apiClient';
import type { PickedImage } from '@/services/multipart';
import { confirmAction, notify } from '@/lib/dialog';
import { formatTime } from '@/lib/format';
import { formatDayMonth } from '@/lib/dates';
import { colors, radius } from '@/theme';
import type { ScheduleTasks, TaskItem, TaskPhase } from '@/types/tasks';

const phaseLabels: Record<TaskPhase, string> = { start: 'Đầu ca', during: 'Trong ca', end: 'Cuối ca' };
const phases: TaskPhase[] = ['start', 'during', 'end'];

/** ViewModel: checklist các ca của tôi hôm nay. */
function useTasks() {
  const [shifts, setShifts] = useState<ScheduleTasks[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async (refresh = false) => {
    if (refresh) setRefreshing(true);
    try {
      setShifts((await taskApi.mine()).data);
      setError(null);
    } catch (err) {
      setError(getErrorMessage(err));
      setShifts((prev) => prev ?? []);
    } finally {
      setRefreshing(false);
    }
  }, []);

  useFocusEffect(
    useCallback(() => {
      void load();
    }, [load]),
  );

  /** Thay checklist của một ca bằng kết quả mới nhất từ máy chủ. */
  const replace = (updated: ScheduleTasks) =>
    setShifts((prev) => prev?.map((s) => (s.scheduleId === updated.scheduleId ? updated : s)) ?? [updated]);

  return { shifts, error, refreshing, reload: load, replace };
}

/** Công việc theo ca (UC 6.2, 6.3): checklist trong ca, tích hoàn thành kèm ghi chú / ảnh hiện trường. */
export function TasksScreen() {
  const vm = useTasks();

  return (
    <ScrollView
      contentContainerStyle={styles.content}
      keyboardShouldPersistTaps="handled"
      refreshControl={<RefreshControl refreshing={vm.refreshing} onRefresh={() => void vm.reload(true)} />}
    >
      <Notice tone="danger" message={vm.error} />
      {vm.shifts === null ? (
        <ActivityIndicator color={colors.primary} style={styles.loading} />
      ) : vm.shifts.length === 0 ? (
        <Card style={styles.empty}>
          <Ionicons name="cafe-outline" size={32} color={colors.muted} />
          <Text style={styles.emptyTitle}>Hôm nay bạn không có ca nào</Text>
          <Text style={styles.muted}>Checklist công việc hiện theo ca làm trong ngày.</Text>
        </Card>
      ) : (
        vm.shifts.map((s) => <ShiftChecklist key={s.scheduleId} shift={s} onChanged={vm.replace} onStale={() => void vm.reload()} />)
      )}
    </ScrollView>
  );
}

function ShiftChecklist({ shift, onChanged, onStale }: { shift: ScheduleTasks; onChanged: (s: ScheduleTasks) => void; onStale: () => void }) {
  const [openId, setOpenId] = useState<number | null>(null);
  const percent = shift.percent ?? 0;

  return (
    <View style={styles.shift}>
      <Card style={styles.shiftHeader}>
        <View style={styles.row}>
          <View style={styles.flex}>
            <Text style={styles.shiftTitle}>{shift.shiftName}</Text>
            <Text style={styles.muted}>
              {formatDayMonth(shift.workDate)} · {shift.startTime}–{shift.endTime}
            </Text>
          </View>
          {shift.isAssigned ? (
            <Text style={styles.percent}>{shift.done}/{shift.total}</Text>
          ) : (
            <Badge label="Chưa vào ca" tone="info" />
          )}
        </View>
        {shift.isAssigned && shift.total > 0 && (
          <View style={styles.bar} accessibilityRole="progressbar" accessibilityValue={{ min: 0, max: 100, now: percent }}>
            <View style={[styles.barFill, { width: `${percent}%` }, percent === 100 && styles.barDone]} />
          </View>
        )}
        {!shift.isAssigned && shift.tasks.length > 0 && (
          <Pressable onPress={() => router.push('/attendance')} style={styles.hintRow} accessibilityRole="button">
            <Ionicons name="finger-print" size={16} color={colors.primary} />
            <Text style={styles.hintText}>Check-in để nhận {shift.tasks.length} việc dưới đây</Text>
          </Pressable>
        )}
        {shift.scheduleStatus === 'completed' && <Text style={styles.muted}>Ca đã check-out — checklist đã khóa.</Text>}
      </Card>

      {shift.tasks.length === 0 ? (
        <Text style={styles.noTasks}>Ca này chưa có việc nào trong checklist.</Text>
      ) : (
        phases.map((phase) => {
          const items = shift.tasks.filter((t) => t.phase === phase);
          if (items.length === 0) return null;
          return (
            <View key={phase} style={styles.phase}>
              <Text style={styles.section}>{phaseLabels[phase]}</Text>
              {items.map((t) => (
                <TaskRow
                  key={t.assignmentId ?? `p${t.taskId}`}
                  task={t}
                  // Việc làm chung do đồng nghiệp đã hoàn thành: chỉ người đó (hoặc Quản lý) bỏ tích được
                  editable={shift.canUpdate && t.assignmentId != null && (t.completedByUserId == null || t.completedByUserId === shift.userId)}
                  onStale={onStale}
                  open={openId === t.assignmentId && t.assignmentId != null}
                  onToggle={() => setOpenId((cur) => (cur === t.assignmentId ? null : t.assignmentId))}
                  onChanged={(s) => {
                    setOpenId(null);
                    onChanged(s);
                  }}
                />
              ))}
            </View>
          );
        })
      )}
    </View>
  );
}

function TaskRow({ task, editable, open, onToggle, onChanged, onStale }: {
  task: TaskItem;
  editable: boolean;
  open: boolean;
  onToggle: () => void;
  onChanged: (s: ScheduleTasks) => void;
  onStale: () => void;
}) {
  const [note, setNote] = useState('');
  const [photo, setPhoto] = useState<PickedImage | null>(null);
  const [busy, setBusy] = useState(false);

  const complete = async () => {
    if (task.requiresPhoto && !photo) return notify('Việc này cần chụp ảnh hiện trường.');
    setBusy(true);
    try {
      const { data, message } = await taskApi.complete(task.assignmentId!, note.trim() || undefined, photo);
      setNote('');
      setPhoto(null);
      onChanged(data);
      notify(message);
    } catch (err) {
      notify(getErrorMessage(err), 'Không thực hiện được');
      if (task.assignMode === 'shared') onStale(); // có thể đồng nghiệp vừa làm xong — tải lại để thấy người làm
    } finally {
      setBusy(false);
    }
  };

  const uncomplete = () =>
    confirmAction('Bỏ tích việc này?', task.proofImageUrl ? 'Ảnh hiện trường đã gửi sẽ bị xóa.' : 'Việc sẽ trở về chưa hoàn thành.', 'Bỏ tích', async () => {
      setBusy(true);
      try {
        onChanged((await taskApi.uncomplete(task.assignmentId!)).data);
      } catch (err) {
        notify(getErrorMessage(err), 'Không thực hiện được');
      } finally {
        setBusy(false);
      }
    });

  return (
    <Card style={[styles.task, task.isCompleted && styles.taskDone]}>
      <Pressable
        onPress={editable ? onToggle : undefined}
        disabled={!editable}
        style={styles.taskRow}
        accessibilityRole="button"
        accessibilityState={{ checked: task.isCompleted, disabled: !editable, expanded: open }}
      >
        <Ionicons
          name={task.isCompleted ? 'checkmark-circle' : 'ellipse-outline'}
          size={26}
          color={task.isCompleted ? colors.success : editable ? colors.primary : colors.border}
        />
        <View style={styles.flex}>
          <Text style={[styles.taskTitle, task.isCompleted && styles.taskTitleDone]}>{task.title}</Text>
          {task.description ? <Text style={styles.muted}>{task.description}</Text> : null}
          <View style={styles.tags}>
            {task.isMandatory && <Badge label="Bắt buộc" tone="warning" />}
            {task.requiresPhoto && <Badge label="Cần ảnh" tone="info" />}
            {task.assignMode === 'shared' && <Badge label="Làm chung" tone="info" />}
            {task.isCompleted && task.completedAt && (
              <Text style={styles.doneAt}>
                {task.assignMode === 'shared' && task.completedByName ? `${task.completedByName} đã làm · ` : ''}Xong lúc {formatTime(task.completedAt)}
              </Text>
            )}
          </View>
          {task.note ? <Text style={styles.note}>“{task.note}”</Text> : null}
        </View>
        {task.proofImageUrl && <Image source={{ uri: task.proofImageUrl }} style={styles.thumb} accessibilityLabel="Ảnh hiện trường" />}
      </Pressable>

      {open && (
        <View style={styles.panel}>
          {task.isCompleted ? (
            <Button title="Bỏ tích (làm lại)" variant="danger" loading={busy} onPress={uncomplete} />
          ) : (
            <>
              <TextField label="Ghi chú" value={note} onChangeText={setNote} placeholder="Không bắt buộc" maxLength={255} />
              <PhotoField label="Ảnh hiện trường" value={photo} onChange={setPhoto} required={task.requiresPhoto} />
              <Button title="Hoàn thành" loading={busy} onPress={complete} icon={<Ionicons name="checkmark" size={20} color={colors.white} />} />
            </>
          )}
        </View>
      )}
    </Card>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  content: { padding: 16, paddingBottom: 40, gap: 16 },
  loading: { marginTop: 32 },
  empty: { alignItems: 'center', gap: 6, paddingVertical: 32 },
  emptyTitle: { fontSize: 16, fontWeight: '700', color: colors.foreground },
  muted: { marginTop: 2, fontSize: 13, lineHeight: 18, color: colors.muted },
  shift: { gap: 10 },
  shiftHeader: { gap: 10 },
  row: { flexDirection: 'row', alignItems: 'center', gap: 12 },
  shiftTitle: { fontSize: 17, fontWeight: '700', color: colors.foreground },
  percent: { fontSize: 20, fontWeight: '800', color: colors.primary },
  bar: { height: 8, borderRadius: radius.full, backgroundColor: colors.background, overflow: 'hidden' },
  barFill: { height: '100%', borderRadius: radius.full, backgroundColor: colors.primary },
  barDone: { backgroundColor: colors.success },
  hintRow: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  hintText: { fontSize: 13, fontWeight: '600', color: colors.primary },
  noTasks: { marginLeft: 4, fontSize: 13, color: colors.muted },
  phase: { gap: 8 },
  section: { marginLeft: 4, fontSize: 13, fontWeight: '700', color: colors.muted, textTransform: 'uppercase' },
  task: { padding: 14 },
  taskDone: { backgroundColor: colors.successSoft, borderColor: '#BBF7D0' },
  taskRow: { flexDirection: 'row', gap: 12, alignItems: 'flex-start' },
  taskTitle: { fontSize: 15, fontWeight: '600', color: colors.foreground },
  taskTitleDone: { color: colors.success },
  tags: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: 6, marginTop: 6 },
  doneAt: { fontSize: 12, color: colors.success, fontWeight: '600' },
  note: { marginTop: 6, fontSize: 13, fontStyle: 'italic', color: colors.foreground },
  thumb: { width: 56, height: 56, borderRadius: radius.sm, backgroundColor: colors.background },
  panel: { marginTop: 12, paddingTop: 12, borderTopWidth: 1, borderTopColor: colors.border, gap: 12 },
});
