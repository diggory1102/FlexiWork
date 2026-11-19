import { api } from './apiClient';
import type { PaginatedResult } from '@/types/api';
import type { Payslip, PayslipDetail } from '@/types/payslip';

/** Phiếu lương của chính tôi — chỉ các kỳ đã chốt. */
export const payslipApi = {
  mine: (params: { year?: number; pageIndex?: number; pageSize?: number } = {}) => api.get<PaginatedResult<Payslip>>('/payslips/me', params),
  get: (id: number) => api.get<PayslipDetail>(`/payslips/${id}`),
};
