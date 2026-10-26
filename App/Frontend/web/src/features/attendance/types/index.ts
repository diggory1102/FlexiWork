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
