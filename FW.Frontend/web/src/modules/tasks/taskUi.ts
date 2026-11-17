import type { BadgeTone } from '@/components/ui/Feedback';
import type { TaskPhase } from '@/types/tasks';

export const phaseLabels: Record<TaskPhase, string> = { start: 'Đầu ca', during: 'Trong ca', end: 'Cuối ca' };
export const phaseTone: Record<TaskPhase, BadgeTone> = { start: 'blue', during: 'gray', end: 'purple' };
export const phaseOptions = (Object.keys(phaseLabels) as TaskPhase[]).map((p) => ({ value: p, label: phaseLabels[p] }));

/** Màu thanh tiến độ: xong hết = xanh lá, còn việc bắt buộc dở = cam, còn lại = xanh dương. */
export function progressVariant(percent: number | null, mandatoryPending: number): 'success' | 'warning' | 'accent' {
  if (percent === 100) return 'success';
  return mandatoryPending > 0 && percent != null ? 'warning' : 'accent';
}
