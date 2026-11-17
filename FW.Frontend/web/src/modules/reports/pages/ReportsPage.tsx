import { useState } from 'react';
import { Tab, TabList } from '@astryxdesign/core/TabList';
import { VStack } from '@astryxdesign/core/Stack';
import { PageHeader } from '@/components/ui/Layout';
import { ViolationsTab } from '../components/ViolationsTab';
import { LeaveUsageTab } from '../components/LeaveUsageTab';

/** Báo cáo (UC 9.x): vi phạm chấm công theo chi nhánh / nhân viên và quỹ phép năm. Admin / HR xem mọi chi nhánh, Quản lý xem chi nhánh mình. */
export function ReportsPage() {
  const [tab, setTab] = useState('violations');

  return (
    <VStack gap={4}>
      <PageHeader title="Báo cáo" description="Vi phạm chấm công và tình hình sử dụng quỹ phép năm." />
      <TabList value={tab} onChange={setTab}>
        <Tab value="violations" label="Vi phạm chấm công" />
        <Tab value="leave" label="Quỹ phép năm" />
      </TabList>
      {tab === 'violations' ? <ViolationsTab /> : <LeaveUsageTab />}
    </VStack>
  );
}
