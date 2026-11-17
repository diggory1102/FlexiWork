import { useState } from 'react';
import { Tab, TabList } from '@astryxdesign/core/TabList';
import { VStack } from '@astryxdesign/core/Stack';
import { PageHeader } from '@/components/ui/Layout';
import { EmployeePayTab } from '../components/EmployeePayTab';
import { AllowancesTab } from '../components/AllowancesTab';
import { PayrollSettingsTab } from '../components/PayrollSettingsTab';

/** Cấu hình lương (Admin / HR): lương từng nhân viên, danh mục phụ cấp và tham số chung. */
export function PayrollConfigPage() {
  const [tab, setTab] = useState('employees');

  return (
    <VStack gap={4}>
      <PageHeader title="Cấu hình lương" description="Thiết lập lương, phụ cấp và hệ số OT làm đầu vào cho bảng lương hằng tháng." />
      <TabList value={tab} onChange={setTab}>
        <Tab value="employees" label="Lương nhân viên" />
        <Tab value="allowances" label="Phụ cấp" />
        <Tab value="settings" label="Tham số chung" />
      </TabList>
      {tab === 'employees' && <EmployeePayTab />}
      {tab === 'allowances' && <AllowancesTab />}
      {tab === 'settings' && <PayrollSettingsTab />}
    </VStack>
  );
}
