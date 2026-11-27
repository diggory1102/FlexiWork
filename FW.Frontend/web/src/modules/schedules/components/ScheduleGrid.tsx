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
import type { EffectiveStaffing, Schedule, Shift } from '@/types/scheduling';
import { countStaffed, scheduleColor, scheduleStateLabel, staffingLabel, staffingState, type StaffingState } from '../scheduleUi';

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
  /** Các ca của chi nhánh — ca có định biên được tổng hợp thành hàng riêng ở cuối lưới. */
  shifts: Shift[];
  /** Định biên hiệu lực (đã áp ngoại lệ theo ngày / thứ) của các ô có giới hạn trong tuần. */
  staffing: EffectiveStaffing[];
  /** Bấm một ô định biên để đặt ngoại lệ cho ca và ngày đó. */
  onStaffing: (shift: Shift, day: string) => void;
  onAdd: (employee: GridEmployee, day: string) => void;
  onOpen: (schedule: Schedule) => void;
}

const stateTone: Record<StaffingState, 'amber' | 'green' | 'blue' | 'red'> = { short: 'amber', ok: 'blue', full: 'green', over: 'red' };

/** Lưới tuần: mỗi hàng một nhân viên, mỗi cột một ngày; ô chứa các ca đã xếp và nút thêm ca (ngày chưa qua). */
export function ScheduleGrid({ days, today, employees, schedules, shifts, staffing, onStaffing, onAdd, onOpen }: Props) {
  const staffed = countStaffed(schedules);
  const limitOf = new Map(staffing.map((e) => [`${e.shiftId}|${e.date}`, e]));
  const limitedShiftIds = new Set(staffing.map((e) => e.shiftId));
  const limited = shifts.filter((s) => limitedShiftIds.has(s.id));
  const byCell = new Map<string, Schedule[]>();
  for (const s of schedules) {
    const key = `${s.userId}|${s.workDate}`;
    byCell.set(key, [...(byCell.get(key) ?? []), s]);
  }

  return (
    <div className="overflow-x-auto">
      <div className="min-w-[1084px]">
        <Table dividers="grid" density="compact" verticalAlign="top">
          <TableHeader>
            <TableRow>
              <TableHeaderCell style={{ width: 300, minWidth: 300 }}>Nhân viên</TableHeaderCell>
              {days.map((day, i) => (
                <TableHeaderCell key={day} style={{ minWidth: 112 }}>
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
                    <Avatar name={emp.fullName} url={emp.avatarUrl} size={24} />
                    <Text weight="medium" maxLines={1}>
                      {emp.fullName}
                    </Text>
                    <span className="shrink-0 whitespace-nowrap">
                      <Text type="supporting">{emp.employeeCode}</Text>
                    </span>
                  </HStack>
                </TableCell>
                {days.map((day) => {
                  const items = byCell.get(`${emp.id}|${day}`) ?? [];
                  return (
                    <TableCell key={day}>
                      <HStack gap={1} vAlign="center" wrap="wrap">
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
                      </HStack>
                    </TableCell>
                  );
                })}
              </TableRow>
            ))}
            {limited.map((shift) => (
              <TableRow key={`staffing-${shift.id}`}>
                <TableCell>
                  <VStack gap={0}>
                    <Text weight="medium" maxLines={1}>
                      Định biên · {shift.shiftCode}
                    </Text>
                    <Text type="supporting">Mặc định: {staffingLabel(shift)}</Text>
                  </VStack>
                </TableCell>
                {days.map((day) => {
                  const count = staffed.get(`${shift.id}|${day}`) ?? 0;
                  const limits = limitOf.get(`${shift.id}|${day}`) ?? { minStaff: null, maxStaff: null, source: 'default' as const, note: null };
                  const state = staffingState(count, limits);
                  const exception = limits.source !== 'default';
                  const upcoming = day >= today;
                  // Ngày đã qua không cảnh báo thiếu người nữa (không xếp thêm được)
                  const tone = !upcoming && state === 'short' ? 'blue' : stateTone[state];
                  const detail =
                    state === 'short'
                      ? `Thiếu ${limits.minStaff! - count} người (cần tối thiểu ${limits.minStaff})`
                      : state === 'full'
                        ? 'Đã đủ người, không xếp thêm được'
                        : state === 'over'
                          ? `Vượt tối đa ${limits.maxStaff} người (định biên bị hạ sau khi đã xếp)`
                          : 'Trong định biên';
                  const source = exception ? ` · Ngoại lệ ${limits.source === 'date' ? 'theo ngày' : 'theo thứ'}${limits.note ? `: ${limits.note}` : ''}` : '';
                  return (
                    <TableCell key={day}>
                      <Tooltip content={`${shift.name}: ${count} người · ${detail}${source}. Bấm để đặt ngoại lệ.`}>
                        <button
                          type="button"
                          className="cursor-pointer text-left"
                          aria-label={`Định biên ${shift.shiftCode} ngày ${formatDayMonth(day)}: ${count} người`}
                          onClick={() => onStaffing(shift, day)}
                        >
                          <VStack gap={0.5} hAlign="start">
                            <Badge tone={tone}>{limits.maxStaff != null ? `${count}/${limits.maxStaff}` : `${count} người`}</Badge>
                            {exception && <Text type="supporting">Ngoại lệ</Text>}
                          </VStack>
                        </button>
                      </Tooltip>
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
