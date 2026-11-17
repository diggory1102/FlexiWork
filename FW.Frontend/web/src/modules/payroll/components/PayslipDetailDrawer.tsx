import { useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { Avatar } from '@/components/ui/Layout';
import { Badge, Notice, Spinner } from '@/components/ui/Feedback';
import { DataTable, type Column } from '@/components/ui/DataTable';
import { DetailDrawer } from '@/components/ui/DetailDrawer';
import { useApiData } from '@/hooks/useApiData';
import { payrollApi } from '@/services/payrollApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { employmentTypeLabels, formatCurrency, formatDateTime, formatNumber } from '@/lib/format';
import type { Payslip, PayslipItem } from '@/types/payroll';
import { itemKindLabels, payslipStatusLabels, payslipStatusTone } from '../payrollUi';

/** Chi tiết một phiếu lương: các dòng (lương, phụ cấp, OT, khoản trừ) và tổng thực nhận; gửi / gửi lại email khi kỳ đã chốt. */
export function PayslipDetailDrawer({ payslip, onClose, onSent }: { payslip: Payslip | null; onClose: () => void; onSent: () => void }) {
  const id = payslip?.id;
  const { data, loading, error, reload } = useApiData(() => payrollApi.payslip(id!), [id], { enabled: id != null, toastError: false });
  const [sending, setSending] = useState(false);
  const p = data?.payslip ?? payslip;

  const send = async () => {
    if (!p) return;
    setSending(true);
    try {
      const { data: result, message } = await payrollApi.send(p.periodId, [p.id]);
      if (result.failed > 0) toast.error(result.failures.map((f) => `${f.fullName}: ${f.reason}`).join(' '));
      else toast.success(message);
      onSent();
      await reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSending(false);
    }
  };

  const columns: Column<PayslipItem>[] = [
    {
      key: 'name',
      header: 'Khoản',
      render: (i) => (
        <VStack gap={0.5}>
          <Text weight="medium">{i.name}</Text>
          <Text type="supporting">{[itemKindLabels[i.kind], i.note].filter(Boolean).join(' · ')}</Text>
        </VStack>
      ),
    },
    { key: 'qty', header: 'Số lượng', align: 'end', render: (i) => formatNumber(i.quantity) },
    { key: 'unit', header: 'Đơn giá', align: 'end', render: (i) => formatCurrency(i.unitAmount) },
    {
      key: 'amount',
      header: 'Thành tiền',
      align: 'end',
      render: (i) => (
        <Text weight="semibold" hasTabularNumbers className={i.amount < 0 ? 'text-red-600' : undefined}>
          {formatCurrency(i.amount)}
        </Text>
      ),
    },
  ];

  const canSend = p?.periodStatus === 'finalized';

  return (
    <DetailDrawer
      open={!!payslip}
      onClose={onClose}
      label="Chi tiết phiếu lương"
      title={`Phiếu lương tháng ${p ? `${String(p.month).padStart(2, '0')}/${p.year}` : ''}`}
      subtitle={p ? `${p.employee.fullName} · ${p.employee.employeeCode}` : undefined}
      width={680}
      footer={
        canSend ? (
          <HStack hAlign="end">
            <Button label={p?.status === 'sent' ? 'Gửi lại email' : 'Gửi email phiếu lương'} variant="primary" isLoading={sending} onClick={send} />
          </HStack>
        ) : undefined
      }
    >
      <VStack gap={4}>
        <Notice title={error} />
        {loading && !data && (
          <div className="flex justify-center py-10">
            <Spinner />
          </div>
        )}
        {p && (
          <>
            <HStack gap={3} vAlign="center" hAlign="between" wrap="wrap">
              <HStack gap={3} vAlign="center">
                <Avatar name={p.employee.fullName} url={p.employee.avatarUrl} size={48} />
                <VStack gap={0.5}>
                  <Heading level={3}>{p.employee.fullName}</Heading>
                  <Text type="supporting">{[employmentTypeLabels[p.employmentType], p.employee.positionTitle, p.branchName].filter(Boolean).join(' · ')}</Text>
                </VStack>
              </HStack>
              <Badge tone={payslipStatusTone[p.status]}>{payslipStatusLabels[p.status]}</Badge>
            </HStack>
            {!canSend && <Notice status="info" title="Kỳ lương chưa chốt" description="Đây là bản nháp, số liệu có thể đổi khi tính lại. Chốt kỳ để gửi phiếu cho nhân viên." />}
            <HStack gap={6} wrap="wrap">
              <VStack gap={0.5}>
                <Text type="supporting">Công thực tế / chuẩn</Text>
                <Text weight="semibold">
                  {formatNumber(p.workDays)} / {formatNumber(p.standardWorkDays)}
                </Text>
              </VStack>
              <VStack gap={0.5}>
                <Text type="supporting">Giờ làm</Text>
                <Text weight="semibold">{formatNumber(p.workHours)}</Text>
              </VStack>
              <VStack gap={0.5}>
                <Text type="supporting">Giờ OT</Text>
                <Text weight="semibold">{formatNumber(p.overtimeHours)}</Text>
              </VStack>
              <VStack gap={0.5}>
                <Text type="supporting">{p.employmentType === 'full_time' ? 'Lương cơ bản' : 'Đơn giá giờ'}</Text>
                <Text weight="semibold">{formatCurrency(p.employmentType === 'full_time' ? p.baseSalary : p.hourlyRate)}</Text>
              </VStack>
            </HStack>
            {data && <DataTable columns={columns} rows={data.items} rowKey={(i) => `${i.kind}-${i.name}-${i.amount}`} minWidth={0} emptyText="Phiếu lương chưa có dòng nào" />}
            <HStack hAlign="between" vAlign="center" className="rounded-lg border border-border p-4">
              <Heading level={3}>Thực nhận</Heading>
              <Heading level={2}>{formatCurrency(p.netPay)}</Heading>
            </HStack>
            {p.sentAt && <Text type="supporting">Đã gửi email lúc {formatDateTime(p.sentAt)}.</Text>}
          </>
        )}
      </VStack>
    </DetailDrawer>
  );
}
