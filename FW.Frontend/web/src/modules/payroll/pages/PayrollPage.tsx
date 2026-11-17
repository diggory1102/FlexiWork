import { useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { EmptyState } from '@astryxdesign/core/EmptyState';
import { Grid } from '@astryxdesign/core/Grid';
import { Icon } from '@astryxdesign/core/Icon';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Text } from '@astryxdesign/core/Text';
import { Banknote, Calculator, Gift, LockKeyhole, Mail, Moon, ReceiptText, TriangleAlert, Users } from 'lucide-react';
import { PageHeader } from '@/components/ui/Layout';
import { Badge, Notice, Spinner } from '@/components/ui/Feedback';
import { ConfirmDialog, Modal } from '@/components/ui/Modal';
import { ExportButton } from '@/components/ui/ExportButton';
import { MonthPicker, currentYearMonth, shiftMonth, type YearMonth } from '@/components/ui/MonthPicker';
import { StatCard } from '@/components/ui/StatCard';
import { useApiData } from '@/hooks/useApiData';
import { payrollApi } from '@/services/payrollApi';
import { getErrorMessage } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import { formatCurrency, formatDateTime } from '@/lib/format';
import type { PayrollSkipped } from '@/types/payroll';
import { PayslipsSection } from '../components/PayslipsSection';
import { periodStatusLabels, periodStatusTone } from '../payrollUi';

type Action = 'calculate' | 'finalize' | 'send';

const monthLabel = ({ year, month }: YearMonth) => `${String(month).padStart(2, '0')}/${year}`;

/**
 * Bảng lương (UC 8.2 – 8.4, Admin / HR): tính từ bảng công đã khóa → chốt kỳ (khóa số liệu) → gửi phiếu lương qua email.
 * Mặc định hiện tháng trước vì tháng hiện tại chưa kết thúc nên chưa chốt được.
 */
export function PayrollPage() {
  const [ym, setYm] = useState<YearMonth>(shiftMonth(currentYearMonth(), -1));
  const [action, setAction] = useState<Action | null>(null);
  const [busy, setBusy] = useState(false);
  const [skipped, setSkipped] = useState<PayrollSkipped[] | null>(null);

  const periods = useApiData(() => payrollApi.periods(ym.year), [ym.year]);
  const period = periods.data?.find((p) => p.month === ym.month) ?? null;
  const detail = useApiData(() => payrollApi.period(period!.id), [period?.id], { enabled: period != null, toastError: false });

  const refresh = async () => {
    await periods.reload();
    await detail.reload();
  };

  const run = async () => {
    if (!action) return;
    setBusy(true);
    try {
      if (action === 'calculate') {
        const { data, message } = await payrollApi.calculate(ym.year, ym.month);
        toast.success(message);
        if (data.skipped.length > 0) setSkipped(data.skipped);
      } else if (action === 'finalize') {
        toast.success((await payrollApi.finalize(period!.id)).message);
      } else {
        const { data, message } = await payrollApi.send(period!.id);
        if (data.failed > 0) setSkipped(data.failures);
        data.failed > 0 && data.sent === 0 ? toast.error(message) : toast.success(message);
      }
      setAction(null);
      await refresh();
    } catch (err) {
      toast.error(getErrorMessage(err));
      setAction(null);
    } finally {
      setBusy(false);
    }
  };

  const totals = detail.data?.totals;
  const readiness = detail.data?.readiness;
  const finalized = period?.status === 'finalized';
  const unsent = period ? period.payslipCount - period.sentCount : 0;

  const texts: Record<Action, { title: string; message: string; confirm: string; tone: 'primary' | 'danger' }> = {
    calculate: {
      title: 'Tính bảng lương',
      message: `Tính lại bảng lương nháp tháng ${monthLabel(ym)} từ các bảng công đã khóa. Phiếu nháp hiện có sẽ được thay mới. Nhân viên chưa cấu hình lương hoặc chưa có bảng công khóa sẽ được liệt kê.`,
      confirm: 'Tính bảng lương',
      tone: 'primary',
    },
    finalize: {
      title: 'Chốt kỳ lương',
      message: `Chốt bảng lương tháng ${monthLabel(ym)}: hệ thống tính lại lần cuối, khóa số liệu và các bảng công của tháng, rồi báo cho nhân viên. Không thể mở lại hay sửa sau khi chốt.`,
      confirm: 'Chốt kỳ lương',
      tone: 'danger',
    },
    send: {
      title: 'Gửi phiếu lương',
      message: `Gửi email phiếu lương tháng ${monthLabel(ym)} cho ${unsent} nhân viên chưa nhận. Nhân viên cũng xem được phiếu trong ứng dụng.`,
      confirm: 'Gửi email',
      tone: 'primary',
    },
  };

  return (
    <VStack gap={4}>
      <PageHeader
        title="Bảng lương"
        description="Tính lương từ bảng công đã khóa, chốt kỳ rồi gửi phiếu lương cho nhân viên."
        actions={
          <>
            <MonthPicker value={ym} onChange={setYm} max={currentYearMonth()} />
            {period && <ExportButton url={payrollApi.exportUrl(period.id)} />}
          </>
        }
      />

      {periods.loading && !periods.data && (
        <div className="flex justify-center py-16">
          <Spinner size="lg" />
        </div>
      )}

      {periods.data && !period && (
        <Card padding={8}>
          <EmptyState
            icon={<Icon icon={ReceiptText} size="lg" color="accent" />}
            title={`Chưa có bảng lương tháng ${monthLabel(ym)}`}
            description="Cần khóa bảng công của tháng (menu Chấm công › Bảng công) và cấu hình lương cho nhân viên trước khi tính."
            actions={<Button label="Tính bảng lương" variant="primary" icon={<Icon icon={Calculator} />} onClick={() => setAction('calculate')} />}
          />
        </Card>
      )}

      {period && (
        <>
          <HStack gap={3} wrap="wrap" vAlign="center" hAlign="between">
            <HStack gap={3} vAlign="center" wrap="wrap">
              <Badge tone={periodStatusTone[period.status]}>{periodStatusLabels[period.status]}</Badge>
              <Text type="supporting">
                {finalized
                  ? `Chốt bởi ${period.finalizedByName ?? '—'} lúc ${formatDateTime(period.finalizedAt)}. Đã gửi ${period.sentCount}/${period.payslipCount} phiếu.`
                  : period.calculatedAt
                    ? `Tính lúc ${formatDateTime(period.calculatedAt)}${period.createdByName ? ` bởi ${period.createdByName}` : ''}.`
                    : ''}
              </Text>
            </HStack>
            <HStack gap={2} wrap="wrap">
              {!finalized && <Button label="Tính lại" variant="secondary" icon={<Icon icon={Calculator} />} onClick={() => setAction('calculate')} />}
              {!finalized && (
                <Button
                  label="Chốt kỳ lương"
                  variant="primary"
                  icon={<Icon icon={LockKeyhole} />}
                  isDisabled={!readiness?.canFinalize}
                  onClick={() => setAction('finalize')}
                />
              )}
              {finalized && (
                <Button label={unsent > 0 ? `Gửi phiếu lương (${unsent})` : 'Đã gửi hết phiếu'} variant="primary" icon={<Icon icon={Mail} />} isDisabled={unsent === 0} onClick={() => setAction('send')} />
              )}
            </HStack>
          </HStack>

          {!finalized && readiness && readiness.problems.length > 0 && (
            <Notice
              status="warning"
              title="Chưa chốt kỳ lương được"
              description={
                <VStack gap={0.5}>
                  {readiness.problems.map((p) => (
                    <span key={p}>• {p}</span>
                  ))}
                </VStack>
              }
            />
          )}

          {totals && (
            <Grid columns={{ minWidth: 170, max: 6 }} gap={3}>
              <StatCard icon={Users} label="Phiếu lương" value={totals.count} tone="accent" hint={`${period.standardWorkDays} công chuẩn`} />
              <StatCard icon={Banknote} label="Lương cơ bản" value={formatCurrency(totals.basic)} tone="success" />
              <StatCard icon={Gift} label="Phụ cấp" value={formatCurrency(totals.allowance)} tone="purple" />
              <StatCard icon={Moon} label="Làm thêm giờ" value={formatCurrency(totals.overtime)} tone="accent" hint={`Hệ số ${period.overtimeMultiplier}`} />
              <StatCard icon={TriangleAlert} label="Khoản trừ" value={formatCurrency(totals.penalty)} tone="error" />
              <StatCard icon={ReceiptText} label="Tổng thực nhận" value={formatCurrency(totals.net)} tone="success" />
            </Grid>
          )}

          <PayslipsSection key={`${period.id}-${period.status}-${period.calculatedAt}`} periodId={period.id} finalized={finalized} onChanged={() => void refresh()} />
        </>
      )}

      <ConfirmDialog
        open={action !== null}
        title={action ? texts[action].title : ''}
        message={action ? texts[action].message : ''}
        confirmText={action ? texts[action].confirm : undefined}
        tone={action ? texts[action].tone : 'primary'}
        loading={busy}
        onConfirm={run}
        onClose={() => setAction(null)}
      />

      <Modal
        open={skipped !== null}
        onClose={() => setSkipped(null)}
        title="Một số nhân viên chưa xử lý được"
        description="Khắc phục rồi tính hoặc gửi lại."
        footer={<Button label="Đã hiểu" variant="primary" onClick={() => setSkipped(null)} />}
      >
        <VStack gap={2}>
          {skipped?.map((s) => (
            <Notice key={s.userId} status="warning" title={`${s.fullName} (${s.employeeCode})`} description={s.reason} />
          ))}
        </VStack>
      </Modal>
    </VStack>
  );
}
