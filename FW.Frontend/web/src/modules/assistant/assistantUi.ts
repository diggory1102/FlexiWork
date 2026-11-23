import type { LucideIcon } from 'lucide-react';
import { CalendarClock, CalendarDays, FileText, Moon, Repeat2 } from 'lucide-react';
import type { AttritionLevel, DraftKind, DraftView } from '@/types/assistant';

/** Câu hiện lúc trợ lý đang gọi công cụ ("đang tra lịch ca…"). */
const TOOL_LABELS: Record<string, string> = {
  get_my_schedule: 'Đang tra lịch ca…',
  get_my_timesheet: 'Đang tra bảng công…',
  get_my_leave_balance: 'Đang tra quỹ phép…',
  get_my_requests: 'Đang tra đơn từ…',
  get_pending_requests: 'Đang tra đơn chờ duyệt…',
  get_attrition_alerts: 'Đang tra nhân sự cần quan tâm…',
  draft_leave_request: 'Đang soạn đơn nghỉ…',
  draft_shift_swap_request: 'Đang soạn đơn đổi ca…',
  draft_overtime_request: 'Đang soạn đơn làm thêm giờ…',
  draft_explanation_request: 'Đang soạn đơn giải trình…',
};

export function toolLabel(name: string): string {
  return TOOL_LABELS[name] ?? 'Đang xử lý…';
}

export const DRAFT_KINDS: Record<DraftKind, { label: string; icon: LucideIcon }> = {
  leave: { label: 'Đơn xin nghỉ', icon: CalendarDays },
  shift_swap: { label: 'Đơn đổi ca', icon: Repeat2 },
  overtime: { label: 'Đơn làm thêm giờ', icon: Moon },
  explanation: { label: 'Đơn giải trình', icon: FileText },
};

export const DRAFT_FALLBACK_ICON = CalendarClock;

/** Bản nháp quá hạn xác nhận (máy chủ hết hạn sau 30 phút) dù trạng thái lưu vẫn là "chờ". */
export function effectiveDraftStatus(draft: DraftView, now = Date.now()): DraftView['status'] {
  return draft.status === 'pending' && new Date(draft.expiresAt).getTime() < now ? 'expired' : draft.status;
}

export const DRAFT_STATUS_TEXT: Record<DraftView['status'], string> = {
  pending: 'Chờ xác nhận',
  confirmed: 'Đã gửi',
  cancelled: 'Đã hủy',
  expired: 'Đã hết hạn',
};

/** Gợi ý bấm nhanh ở màn hình trống; Quản lý / HR có thêm câu về đơn chờ duyệt và nhân sự cần quan tâm. */
export function suggestionsFor(isManager: boolean): string[] {
  const common = [
    'Em còn mấy ngày phép?',
    'Tuần này em làm ca nào?',
    'Cho em xin nghỉ ngày mai vì có việc gia đình',
    'Đăng ký giúp em OT tối mai từ 18h đến 21h để kiểm kho',
  ];
  return isManager ? ['Có đơn nào đang chờ tôi duyệt không?', 'Ai trong chi nhánh có dấu hiệu muốn nghỉ việc?', ...common.slice(0, 2)] : common;
}

export const ATTRITION_LEVEL: Record<AttritionLevel, { label: string; tone: 'gray' | 'amber' | 'red' }> = {
  low: { label: 'Thấp', tone: 'gray' },
  medium: { label: 'Trung bình', tone: 'amber' },
  high: { label: 'Cao', tone: 'red' },
};
