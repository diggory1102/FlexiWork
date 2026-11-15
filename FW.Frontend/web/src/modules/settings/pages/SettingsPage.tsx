import { VStack } from '@astryxdesign/core/Stack';
import { PageHeader } from '@/components/ui/Layout';
import { AttendancePolicyCard } from '../components/AttendancePolicyCard';

/** Cài đặt hệ thống (Admin / HR). Cấp 4 sẽ bổ sung loại đơn từ & quỹ phép. */
export function SettingsPage() {
  return (
    <VStack maxWidth={1080}>
      <PageHeader title="Cài đặt" description="Tham số áp dụng cho toàn công ty." />
      <AttendancePolicyCard />
    </VStack>
  );
}
