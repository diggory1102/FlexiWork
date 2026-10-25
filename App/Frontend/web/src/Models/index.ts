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

export interface AttendanceCheckinRequest {
  userId: number;
  branchId: number;
  scheduleId?: number;
  latitude: number;
  longitude: number;
  bssid?: string;
  imageFile: File;
}

export interface AttendanceResult {
  id: number;
  status: 'valid' | 'pending_approval' | 'invalid';
  checkinTime: string;
  distance: number;
  isFaceMatched: boolean;
  confidence: number;
  message?: string;
}
