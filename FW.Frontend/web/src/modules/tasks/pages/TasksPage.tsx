import { useState } from 'react';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Tab, TabList } from '@astryxdesign/core/TabList';
import { PageHeader } from '@/components/ui/Layout';
import { TaskProgressTab } from '../components/TaskProgressTab';
import { TaskTemplatesTab } from '../components/TaskTemplatesTab';

/** Công việc theo ca (Task 4.5, UC 6.1 / 6.4): theo dõi tiến độ checklist trong ngày và quản lý mẫu checklist của từng ca. */
export function TasksPage() {
  const [tab, setTab] = useState('progress');

  return (
    <>
      <PageHeader
        title="Công việc theo ca"
        description="Mỗi nhân viên làm ca (đúng chi nhánh và chức vụ của việc) đều nhận một bản checklist khi check-in; việc thêm giữa ca được giao ngay cho người đang làm. Theo dõi tiến độ trong ngày và quản lý mẫu việc của từng ca."
      />
      <VStack gap={3}>
        <HStack>
          <TabList value={tab} onChange={setTab}>
            <Tab value="progress" label="Tiến độ trong ngày" />
            <Tab value="templates" label="Mẫu checklist" />
          </TabList>
        </HStack>
        {tab === 'progress' ? <TaskProgressTab /> : <TaskTemplatesTab />}
      </VStack>
    </>
  );
}
