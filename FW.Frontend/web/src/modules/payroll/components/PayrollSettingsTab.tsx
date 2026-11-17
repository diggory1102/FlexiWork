import { useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { MetadataList, MetadataListItem } from '@astryxdesign/core/MetadataList';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Heading, Text } from '@astryxdesign/core/Text';
import { FormError, Spinner } from '@/components/ui/Feedback';
import { useApiData } from '@/hooks/useApiData';
import { useFormSubmit } from '@/hooks/useFormSubmit';
import { payrollConfigApi } from '@/services/payrollApi';
import { formatNumber } from '@/lib/format';

/** Tham số chung của bảng lương: hệ số OT (chỉnh được); số công chuẩn và giờ chuẩn mỗi ngày chỉnh ở Cài đặt › Quy tắc chấm công. */
export function PayrollSettingsTab() {
  const { data, loading, reload } = useApiData(() => payrollConfigApi.settings(), []);
  const [multiplier, setMultiplier] = useState<number | null>(null);
  const { saving, error, setError, run } = useFormSubmit();

  useEffect(() => {
    if (data) setMultiplier(data.overtimeMultiplier);
  }, [data]);

  const save = () => {
    if (multiplier == null || multiplier < 1 || multiplier > 5) return setError('Hệ số OT phải từ 1 đến 5.');
    void run(async () => {
      await payrollConfigApi.updateSettings(multiplier);
      void reload();
      return 'Đã lưu hệ số OT. Áp dụng cho các kỳ lương tính sau.';
    });
  };

  if (loading && !data) {
    return (
      <div className="flex justify-center py-16">
        <Spinner size="lg" />
      </div>
    );
  }

  return (
    <Card padding={6} maxWidth={640}>
      <VStack gap={5}>
        <VStack gap={1}>
          <Heading level={3}>Tham số bảng lương</Heading>
          <Text type="supporting">Áp dụng cho toàn doanh nghiệp khi tính lương.</Text>
        </VStack>
        <FormError message={error} />
        <NumberInput
          label="Hệ số tiền làm thêm giờ (OT)"
          description="Tiền OT = số giờ OT × đơn giá giờ × hệ số. Toàn thời gian: đơn giá giờ = lương tháng ÷ số công chuẩn ÷ giờ chuẩn mỗi ngày."
          min={1}
          max={5}
          step={0.1}
          units="lần"
          value={multiplier}
          onChange={(v) => setMultiplier(v)}
        />
        <MetadataList>
          <MetadataListItem label="Số công chuẩn mỗi tháng">{data ? `${formatNumber(data.standardWorkDays)} công` : '—'}</MetadataListItem>
          <MetadataListItem label="Giờ chuẩn mỗi ngày công">{data ? `${formatNumber(data.standardHoursPerDay)} giờ` : '—'}</MetadataListItem>
        </MetadataList>
        <HStack hAlign="end">
          <Button label="Lưu" variant="primary" isLoading={saving} onClick={save} />
        </HStack>
      </VStack>
    </Card>
  );
}
