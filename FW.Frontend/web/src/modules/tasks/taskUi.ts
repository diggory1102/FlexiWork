import type { BadgeTone } from '@/components/ui/Feedback';
import type { TaskAssignMode, TaskPhase } from '@/types/tasks';

export const phaseLabels: Record<TaskPhase, string> = { start: 'Đầu ca', during: 'Trong ca', end: 'Cuối ca' };
export const phaseTone: Record<TaskPhase, BadgeTone> = { start: 'blue', during: 'gray', end: 'purple' };
export const phaseOptions = (Object.keys(phaseLabels) as TaskPhase[]).map((p) => ({ value: p, label: phaseLabels[p] }));

export const assignModeLabels: Record<TaskAssignMode, string> = { each: 'Mỗi người một bản', shared: 'Làm chung' };
export const assignModeHints: Record<TaskAssignMode, string> = {
  each: 'Mỗi nhân viên làm ca đều có bản việc riêng và phải tự bấm hoàn thành. Hợp với việc cá nhân: kiểm đếm két của mình, nhận đồng phục…',
  shared: 'Cả ca cùng thấy một việc; ai làm thì bấm hoàn thành, một người bấm là cả ca xong và những người còn lại không phải làm nữa. Hợp với việc chung: mở cửa, lau sàn, sắp xếp kệ…',
};
export const assignModeOptions = (Object.keys(assignModeLabels) as TaskAssignMode[]).map((m) => ({
  value: m,
  label: m === 'each' ? 'Mỗi người một bản (ai cũng phải làm)' : 'Làm chung (một người xong là cả ca xong)',
}));

/** Màu thanh tiến độ: xong hết = xanh lá, còn việc bắt buộc dở = cam, còn lại = xanh dương. */
export function progressVariant(percent: number | null, mandatoryPending: number): 'success' | 'warning' | 'accent' {
  if (percent === 100) return 'success';
  return mandatoryPending > 0 && percent != null ? 'warning' : 'accent';
}
