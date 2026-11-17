import { useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Grid } from '@astryxdesign/core/Grid';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Switch } from '@astryxdesign/core/Switch';
import { Heading, Text } from '@astryxdesign/core/Text';
import { Avatar } from '@/components/ui/Layout';
import { Badge, FormError, Notice, Spinner } from '@/components/ui/Feedback';
import { DetailDrawer } from '@/components/ui/DetailDrawer';
import { useApiData } from '@/hooks/useApiData';
import { useFormSubmit } from '@/hooks/useFormSubmit';
import { payrollConfigApi } from '@/services/payrollApi';
import { employmentTypeLabels, formatCurrency } from '@/lib/format';
import type { EmployeeAllowance, EmployeePay } from '@/types/payroll';
import { basisUnit } from '../payrollUi';

interface AllowanceDraft {
  assigned: boolean;
  /** Mức riêng (null = dùng mức danh mục). */
  amount: number | null;
}

/** Ngăn chi tiết lương một nhân viên: lương cơ bản / đơn giá giờ riêng và phụ cấp được hưởng (lưu riêng từng phần). */
export function EmployeePayDrawer({ employee, onClose, onChanged }: { employee: EmployeePay | null; onClose: () => void; onChanged: () => void }) {
  const userId = employee?.userId;
  const { data, loading, error, reload } = useApiData(() => payrollConfigApi.employee(userId!), [userId], { enabled: userId != null, toastError: false });
  const [baseSalary, setBaseSalary] = useState<number | null>(null);
  const [hourlyRate, setHourlyRate] = useState<number | null>(null);
  const [drafts, setDrafts] = useState<Record<number, AllowanceDraft>>({});
  const payForm = useFormSubmit();
  const allowanceForm = useFormSubmit();

  useEffect(() => {
    if (!data) return;
    setBaseSalary(data.pay.baseSalary);
    setHourlyRate(data.pay.hourlyRate);
    setDrafts(Object.fromEntries(data.allowances.map((a) => [a.allowanceId, { assigned: a.assigned, amount: a.overrideAmount }])));
    payForm.setError(null);
    allowanceForm.setError(null);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data]);

  const pay = data?.pay ?? employee;
  const fullTime = pay?.employmentType === 'full_time';
  const editable = (data?.allowances ?? []).filter((a) => !a.applyToAll);
  const forEveryone = (data?.allowances ?? []).filter((a) => a.applyToAll);

  const savePay = () => {
    if (userId == null) return;
    if (fullTime && (baseSalary == null || baseSalary <= 0)) return payForm.setError('Nhân viên toàn thời gian cần có lương cơ bản lớn hơn 0.');
    void payForm.run(
      async () => {
        await payrollConfigApi.updateEmployeePay(userId, { baseSalary, hourlyRate });
        return 'Đã lưu lương.';
      },
      () => {
        onChanged();
        void reload();
      },
    );
  };

  const saveAllowances = () => {
    if (userId == null) return;
    const items = editable.filter((a) => drafts[a.allowanceId]?.assigned).map((a) => ({ allowanceId: a.allowanceId, amount: drafts[a.allowanceId].amount }));
    void allowanceForm.run(
      async () => {
        await payrollConfigApi.setEmployeeAllowances(userId, items);
        return 'Đã lưu phụ cấp.';
      },
      () => {
        onChanged();
        void reload();
      },
    );
  };

  const setDraft = (id: number, change: Partial<AllowanceDraft>) => setDrafts((d) => ({ ...d, [id]: { ...d[id], ...change } }));

  return (
    <DetailDrawer
      open={!!employee}
      onClose={onClose}
      label="Lương nhân viên"
      title="Lương nhân viên"
      subtitle={employee ? `${employee.fullName} · ${employee.employeeCode}` : undefined}
      width={640}
    >
      <VStack gap={5}>
        <Notice title={error} />
        {loading && !data && (
          <div className="flex justify-center py-10">
            <Spinner />
          </div>
        )}
        {pay && (
          <HStack gap={3} vAlign="center" hAlign="between" wrap="wrap">
            <HStack gap={3} vAlign="center">
              <Avatar name={pay.fullName} url={pay.avatarUrl} size={48} />
              <VStack gap={0.5}>
                <Heading level={3}>{pay.fullName}</Heading>
                <Text type="supporting">{[employmentTypeLabels[pay.employmentType], pay.positionTitle, pay.branchName].filter(Boolean).join(' · ')}</Text>
              </VStack>
            </HStack>
            <Badge tone={pay.isConfigured ? 'green' : 'amber'}>{pay.isConfigured ? 'Đủ thông tin' : 'Thiếu thông tin'}</Badge>
          </HStack>
        )}

        {data && (
          <>
            <Card padding={5}>
              <VStack gap={4}>
                <Heading level={3}>Mức lương</Heading>
                <FormError message={payForm.error} />
                <NumberInput
                  label="Lương cơ bản mỗi tháng"
                  isRequired={fullTime}
                  units="VNĐ"
                  min={0}
                  step={100000}
                  isIntegerOnly
                  hasClear
                  placeholder={fullTime ? 'Bắt buộc với toàn thời gian' : 'Không dùng cho bán thời gian'}
                  description={baseSalary ? formatCurrency(baseSalary) : 'Nhập số tiền theo VNĐ.'} // luôn có dòng mô tả để bố cục không dịch khi nhập xong
                  value={baseSalary}
                  onChange={(v) => setBaseSalary(v ?? null)}
                />
                <NumberInput
                  label="Đơn giá giờ riêng"
                  isRequired={!fullTime && !data.pay.positionHourlyRate}
                  units="VNĐ / giờ"
                  min={0}
                  step={1000}
                  isIntegerOnly
                  hasClear
                  placeholder={data.pay.positionHourlyRate ? `Theo chức vụ: ${formatCurrency(data.pay.positionHourlyRate)}` : 'Chưa có đơn giá theo chức vụ'}
                  description={fullTime ? 'Toàn thời gian: dùng tính OT khi để trống sẽ lấy theo lương cơ bản.' : 'Để trống để dùng đơn giá theo chức vụ.'}
                  value={hourlyRate}
                  onChange={(v) => setHourlyRate(v ?? null)}
                />
                <Grid columns={2} gap={3}>
                  <VStack gap={0.5}>
                    <Text type="supporting">Đơn giá giờ đang áp dụng</Text>
                    <Text weight="semibold" hasTabularNumbers>
                      {formatCurrency(data.pay.effectiveHourlyRate)}
                    </Text>
                  </VStack>
                  <VStack gap={0.5}>
                    <Text type="supporting">Đơn giá giờ tính OT (chưa nhân hệ số)</Text>
                    <Text weight="semibold" hasTabularNumbers>
                      {formatCurrency(data.pay.overtimeHourlyRate)}
                    </Text>
                  </VStack>
                </Grid>
                <HStack hAlign="end">
                  <Button label="Lưu lương" variant="primary" isLoading={payForm.saving} onClick={savePay} />
                </HStack>
              </VStack>
            </Card>

            <Card padding={5}>
              <VStack gap={4}>
                <Heading level={3}>Phụ cấp</Heading>
                <FormError message={allowanceForm.error} />
                {forEveryone.length > 0 && (
                  <VStack gap={2}>
                    <Text type="supporting">Áp dụng cho tất cả nhân viên</Text>
                    {forEveryone.map((a) => (
                      <AllowanceLine key={a.allowanceId} allowance={a} />
                    ))}
                  </VStack>
                )}
                {editable.length === 0 ? (
                  <Text type="supporting">Chưa có phụ cấp nào gán riêng được. Thêm ở tab “Phụ cấp”.</Text>
                ) : (
                  <VStack gap={3}>
                    <Text type="supporting">Gán riêng</Text>
                    {editable.map((a) => {
                      const draft = drafts[a.allowanceId] ?? { assigned: false, amount: null };
                      return (
                        <HStack key={a.allowanceId} gap={3} vAlign="center" hAlign="between" wrap="wrap">
                          <Switch
                            label={a.name}
                            description={`${formatCurrency(a.defaultAmount)} ${basisUnit[a.basis]}${a.isActive ? '' : ' · tạm ngừng'}`}
                            value={draft.assigned}
                            onChange={(assigned) => setDraft(a.allowanceId, { assigned })}
                          />
                          {draft.assigned && (
                            <NumberInput
                              label="Mức riêng"
                              isLabelHidden
                              width={190}
                              units="VNĐ"
                              min={0}
                              step={1000}
                              isIntegerOnly
                              hasClear
                              placeholder="Theo danh mục"
                              value={draft.amount}
                              onChange={(v) => setDraft(a.allowanceId, { amount: v ?? null })}
                            />
                          )}
                        </HStack>
                      );
                    })}
                    <HStack hAlign="end">
                      <Button label="Lưu phụ cấp" variant="primary" isLoading={allowanceForm.saving} onClick={saveAllowances} />
                    </HStack>
                  </VStack>
                )}
              </VStack>
            </Card>
          </>
        )}
      </VStack>
    </DetailDrawer>
  );
}

function AllowanceLine({ allowance: a }: { allowance: EmployeeAllowance }) {
  return (
    <HStack hAlign="between" vAlign="center" gap={2}>
      <Text>{a.name}</Text>
      <Text type="supporting" hasTabularNumbers>
        {formatCurrency(a.effectiveAmount)} {basisUnit[a.basis]}
      </Text>
    </HStack>
  );
}
