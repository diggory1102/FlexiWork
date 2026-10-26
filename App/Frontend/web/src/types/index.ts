export interface Branch {
  id: number;
  branchCode: string;
  name: string;
  address: string;
  phone?: string;
  email?: string;
  latitude: number;
  longitude: number;
  radius: number;
  status: string;
}

export interface User {
  id: number;
  employeeCode: string;
  email: string;
  fullName: string;
  phoneNumber?: string;
  avatarUrl?: string;
  branchId?: number;
  departmentId?: number;
  positionId?: number;
  status: string;
}
