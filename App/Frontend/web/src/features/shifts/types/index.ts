export interface Shift {
  id: number;
  branchId: number;
  shiftCode: string;
  name: string;
  startTime: string;
  endTime: string;
  breakMinutes: number;
  workUnits: number;
  status: string;
}
