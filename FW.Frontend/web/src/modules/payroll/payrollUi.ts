import type { BadgeTone } from '@/components/ui/Feedback';
import type { AllowanceBasis, PayrollPeriodStatus, PayslipItemKind, PayslipStatus } from '@/types/payroll';

export const basisLabels: Record<AllowanceBasis, string> = {
  monthly: 'Cố định mỗi tháng',
  night_shift: 'Theo số ca đêm',
  split_day: 'Theo số ngày ca gãy',
};

export const basisOptions = (Object.keys(basisLabels) as AllowanceBasis[]).map((b) => ({ value: b, label: basisLabels[b] }));

/** Đơn vị tính của mức tiền theo cách tính phụ cấp. */
export const basisUnit: Record<AllowanceBasis, string> = {
  monthly: '/ tháng',
  night_shift: '/ ca đêm',
  split_day: '/ ngày ca gãy',
};

export const periodStatusLabels: Record<PayrollPeriodStatus, string> = { draft: 'Bản nháp', finalized: 'Đã chốt' };
export const periodStatusTone: Record<PayrollPeriodStatus, BadgeTone> = { draft: 'amber', finalized: 'green' };

export const payslipStatusLabels: Record<PayslipStatus, string> = { draft: 'Chưa gửi', sent: 'Đã gửi' };
export const payslipStatusTone: Record<PayslipStatus, BadgeTone> = { draft: 'gray', sent: 'green' };

export const itemKindLabels: Record<PayslipItemKind, string> = {
  basic: 'Lương',
  allowance: 'Phụ cấp',
  overtime: 'Làm thêm giờ',
  penalty: 'Khoản trừ',
  adjustment: 'Điều chỉnh',
};
