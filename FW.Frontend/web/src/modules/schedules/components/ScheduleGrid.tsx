import { Icon } from '@astryxdesign/core/Icon';
import { IconButton } from '@astryxdesign/core/IconButton';
import { HStack, VStack } from '@astryxdesign/core/Stack';
import { Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow } from '@astryxdesign/core/Table';
import { Text } from '@astryxdesign/core/Text';
import { Token } from '@astryxdesign/core/Token';
import { Tooltip } from '@astryxdesign/core/Tooltip';
import { Plus } from 'lucide-react';
import { Avatar } from '@/components/ui/Layout';
import { Badge } from '@/components/ui/Feedback';
import { formatDayMonth, weekdayLabels } from '@/lib/dates';
import type { Schedule } from '@/types/scheduling';
import { scheduleColor, scheduleStateLabel } from '../scheduleUi';

export interface GridEmployee {
  id: number;
  fullName: string;
  employeeCode: string;
  avatarUrl?: string | null;
}

interface Props {
  days: string[];
  today: string;
  employees: GridEmployee[];
  schedules: Schedule[];
  onAdd: (employee: GridEmployee, day: string) => void;
  onOpen: (schedule: Schedule) => void;
}

/** Lưới tuần: mỗi hàng một nhân viên, mỗi cột một ngày; ô chứa các ca đã xếp và nút thêm ca (ngày chưa qua). */
export function ScheduleGrid({ days, today, employees, schedules, onAdd, onOpen }: Props) {
  const byCell = new Map<string, Schedule[]>();
  for (const s of schedules) {
    const key = `${s.userId}|${s.workDate}`;
    byCell.set(key, [...(byCell.get(key) ?? []), s]);
  }

  return (
    <div className="overflow-x-auto">
      <div className="min-w-[1080px]">
        <Table dividers="grid" density="compact" verticalAlign="top">
          <TableHeader>
            <TableRow>
              <TableHeaderCell style={{ width: 220, minWidth: 220 }}>Nhân viên</TableHeaderCell>
              {days.map((day, i) => (
                <TableHeaderCell key={day} style={{ minWidth: 120 }}>
                  <HStack gap={1} vAlign="center" wrap="wrap">
                    <Text weight="semibold" color={day === today ? 'accent' : undefined}>
                      {weekdayLabels[i]}
                    </Text>
                    <Text type="supporting" hasTabularNumbers>
                      {formatDayMonth(day)}
                    </Text>
                    {day === today && <Badge tone="blue">Hôm nay</Badge>}
                  </HStack>
                </TableHeaderCell>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {employees.map((emp) => (
              <TableRow key={emp.id}>
                <TableCell>
                  <HStack gap={2} vAlign="center">
                    <Avatar name={emp.fullName} url={emp.avatarUrl} size={32} />
                    <VStack gap={0}>
                      <Text weight="medium" maxLines={1}>
                        {emp.fullName}
                      </Text>
                      <Text type="supporting">{emp.employeeCode}</Text>
                    </VStack>
                  </HStack>
                </TableCell>
                {days.map((day) => {
                  const items = byCell.get(`${emp.id}|${day}`) ?? [];
                  return (
                    <TableCell key={day}>
                      <VStack gap={1} hAlign="start">
                        {items.map((s) => (
                          <Tooltip
                            key={s.id}
                            content={`${s.shiftName} · ${s.startTime}–${s.endTime}${s.isCrossDay ? ' (qua đêm)' : ''} · ${scheduleStateLabel(s)}`}
                          >
                            <Token
                              size="sm"
                              color={scheduleColor(s)}
                              label={s.status === 'cancelled' ? `${s.shiftCode} (hủy)` : s.shiftCode}
                              onClick={() => onOpen(s)}
                            />
                          </Tooltip>
                        ))}
                        {day >= today && (
                          <IconButton
                            variant="ghost"
                            size="sm"
                            label={`Xếp ca cho ${emp.fullName} ngày ${formatDayMonth(day)}`}
                            tooltip="Xếp ca"
                            icon={<Icon icon={Plus} size="sm" />}
                            onClick={() => onAdd(emp, day)}
                          />
                        )}
                      </VStack>
                    </TableCell>
                  );
                })}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </div>
  );
}
