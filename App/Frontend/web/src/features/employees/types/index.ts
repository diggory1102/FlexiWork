export interface EmployeeProfile {
  id: number;
  userId: number;
  employeeCode: string;
  fullName: string;
  email: string;
  phone?: string;
  departmentName?: string;
  positionName?: string;
  baseSalary: number;
  status: string;
}
