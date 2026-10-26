export interface MySchedule {
  id: number;
  date: string;
  shiftName: string;
  startTime: string;
  endTime: string;
  coefficient: number;
  status: 'assigned' | 'completed' | 'absent';
}
