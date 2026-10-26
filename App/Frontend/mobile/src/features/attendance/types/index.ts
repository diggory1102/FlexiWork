export interface CheckinPayload {
  userId: number;
  branchId: number;
  latitude: number;
  longitude: number;
  bssid?: string;
  imageUri: string;
}

export interface CheckinResult {
  id: number;
  status: 'valid' | 'pending_approval' | 'invalid';
  checkinTime: string;
  distance: number;
  isFaceMatched: boolean;
  confidence: number;
  message?: string;
}
