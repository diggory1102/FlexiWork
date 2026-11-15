import { useEffect, useState } from 'react';
import { Button } from '@astryxdesign/core/Button';
import { Card } from '@astryxdesign/core/Card';
import { Divider } from '@astryxdesign/core/Divider';
import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { NumberInput } from '@astryxdesign/core/NumberInput';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow } from '@astryxdesign/core/Table';
import { Heading, Text } from '@astryxdesign/core/Text';
import { TextInput } from '@astryxdesign/core/TextInput';
import { Plus, Trash2 } from 'lucide-react';
import { EmptyState, ErrorList, Spinner, type ErrorDetail } from '@/components/ui/Feedback';
import { attendancePolicyApi } from '@/services/schedulingApi';
import { getErrorDetail } from '@/services/apiClient';
import { toast } from '@/stores/toastStore';
import type { AttendancePolicyRequest, ViolationType } from '@/types/scheduling';

type Tier = AttendancePolicyRequest['rules'][number] & { key: number };

const sections: { type: ViolationType; title: string; hint: string; example: string }[] = [
  { type: 'late', title: 'Đi muộn', hint: 'Tính từ giờ bắt đầu ca, khi đã vượt số phút cho phép của ca.', example: 'VD: Đi muộn từ 15 phút' },
  { type: 'early_leave', title: 'Về sớm', hint: 'Tính tới giờ kết thúc ca, khi đã vượt số phút cho phép của ca.', example: 'VD: Về sớm từ 15 phút' },
];

let nextKey = 1;

/**
 * Số công chuẩn tháng & quy tắc trừ công / phạt theo bậc (UC 1.5). Cả bộ quy tắc được lưu một lần (thay toàn bộ).
 */
export function AttendancePolicyCard() {
  const [standardWorkDays, setStandardWorkDays] = useState(26);
  const [tiers, setTiers] = useState<Tier[]>([]);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<ErrorDetail | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const { data } = await attendancePolicyApi.get();
      setStandardWorkDays(data.standardWorkDays);
      setTiers(data.rules.map(({ id: _id, ...rule }) => ({ ...rule, key: nextKey++ })));
    } catch (err) {
      setError(getErrorDetail(err));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => void load(), []);

  const update = (key: number, changes: Partial<Tier>) => setTiers((list) => list.map((t) => (t.key === key ? { ...t, ...changes } : t)));
  const remove = (key: number) => setTiers((list) => list.filter((t) => t.key !== key));
  const add = (type: ViolationType) =>
    setTiers((list) => {
      const last = list.filter((t) => t.violationType === type).sort((a, b) => a.minMinutes - b.minMinutes).at(-1);
      return [
        ...list,
        {
          key: nextKey++,
          violationType: type,
          minMinutes: last ? last.minMinutes + 15 : 1,
          deductWorkUnits: last?.deductWorkUnits ?? 0,
          penaltyAmount: last?.penaltyAmount ?? 20_000,
          description: null,
        },
      ];
    });

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      const { data, message } = await attendancePolicyApi.update({
        standardWorkDays,
        rules: tiers.map(({ key: _key, ...rule }) => rule),
      });
      setStandardWorkDays(data.standardWorkDays);
      setTiers(data.rules.map(({ id: _id, ...rule }) => ({ ...rule, key: nextKey++ })));
      toast.success(message);
    } catch (err) {
      setError(getErrorDetail(err));
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <Card padding={6}>
        <HStack hAlign="center">
          <Spinner />
        </HStack>
      </Card>
    );
  }

  return (
    <Card padding={6}>
      <VStack gap={5}>
        <VStack gap={1}>
          <Heading level={2}>Quy tắc chấm công</Heading>
          <Text type="supporting" as="p">
            Mỗi loại vi phạm có nhiều bậc "từ X phút". Hệ thống chỉ áp dụng <b>bậc cao nhất</b> mà số phút vi phạm đạt tới (không cộng dồn
            các bậc). Bậc sau không được nhẹ hơn bậc trước. Số phút cho phép đi muộn / về sớm cài đặt riêng cho từng ca ở trang Khung ca.
          </Text>
        </VStack>

        <ErrorList error={error} />

        <NumberInput
          label="Số công chuẩn trong tháng"
          description="Lương nhân viên toàn thời gian = lương tháng ÷ số công chuẩn × số ngày công thực tế."
          units="công"
          min={1}
          max={31}
          step={0.5}
          width={440}
          value={standardWorkDays}
          onChange={(v) => setStandardWorkDays(v ?? 26)}
        />

        {sections.map((section) => {
          const rows = tiers.filter((t) => t.violationType === section.type).sort((a, b) => a.minMinutes - b.minMinutes);
          return (
            <VStack key={section.type} gap={3}>
              <Divider />
              <HStack gap={2} wrap="wrap" hAlign="between" vAlign="center">
                <VStack gap={0.5}>
                  <Heading level={3}>{section.title}</Heading>
                  <Text type="supporting">{section.hint}</Text>
                </VStack>
                <Button label="Thêm bậc" size="sm" icon={<Icon icon={Plus} />} onClick={() => add(section.type)} />
              </HStack>

              {rows.length === 0 ? (
                <EmptyState title={`Không xử lý ${section.title.toLowerCase()}`} description="Chưa có bậc nào: vi phạm chỉ được ghi nhận, không trừ công hay phạt tiền." />
              ) : (
                <Table density="compact" verticalAlign="top">
                  <TableHeader>
                    <TableRow>
                      <TableHeaderCell style={{ width: 150, minWidth: 150 }}>Từ (phút)</TableHeaderCell>
                      <TableHeaderCell style={{ width: 150, minWidth: 150 }}>Trừ công</TableHeaderCell>
                      <TableHeaderCell style={{ width: 200, minWidth: 200 }}>Phạt tiền</TableHeaderCell>
                      <TableHeaderCell style={{ minWidth: 220 }}>Ghi chú</TableHeaderCell>
                      <TableHeaderCell style={{ width: 56, minWidth: 56 }}>{''}</TableHeaderCell>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {rows.map((t) => (
                      <TableRow key={t.key}>
                        <TableCell>
                          <NumberInput label="Từ (phút)" isLabelHidden units="phút" min={1} max={600} isIntegerOnly value={t.minMinutes} onChange={(v) => update(t.key, { minMinutes: v ?? 1 })} />
                        </TableCell>
                        <TableCell>
                          <NumberInput label="Trừ công" isLabelHidden units="công" min={0} max={3} step={0.25} value={t.deductWorkUnits} onChange={(v) => update(t.key, { deductWorkUnits: v ?? 0 })} />
                        </TableCell>
                        <TableCell>
                          <NumberInput
                            label="Phạt tiền"
                            isLabelHidden
                            units="đ"
                            min={0}
                            max={10_000_000}
                            step={10_000}
                            isIntegerOnly
                            value={t.penaltyAmount}
                            onChange={(v) => update(t.key, { penaltyAmount: v ?? 0 })}
                          />
                        </TableCell>
                        <TableCell>
                          <TextInput label="Ghi chú" isLabelHidden placeholder={section.example} value={t.description ?? ''} onChange={(v) => update(t.key, { description: v || null })} />
                        </TableCell>
                        <TableCell>
                          <IconButton variant="ghost" size="sm" label="Xóa bậc" tooltip="Xóa bậc" icon={<Icon icon={Trash2} color="error" />} onClick={() => remove(t.key)} />
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </VStack>
          );
        })}

        <Divider />
        <HStack gap={2} hAlign="end">
          <Button label="Hoàn tác" variant="secondary" onClick={() => void load()} isDisabled={saving} />
          <Button label="Lưu quy tắc" variant="primary" onClick={save} isLoading={saving} />
        </HStack>
      </VStack>
    </Card>
  );
}
