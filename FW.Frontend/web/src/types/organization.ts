import type { PagedQuery } from './api';

export type BranchStatus = 'active' | 'inactive';

export interface Branch {
  id: number;
  branchCode: string;
  name: string;
  address: string;
  phone: string | null;
  email: string | null;
  latitude: number;
  longitude: number;
  radius: number;
  status: BranchStatus;
  createdAt: string;
  updatedAt: string | null;
}

export interface BranchWifi {
  id: number;
  branchId: number;
  ssid: string;
  bssid: string;
  isActive: boolean;
  createdAt: string;
}

export interface BranchDetail {
  branch: Branch;
  wifis: BranchWifi[];
}

export interface BranchFilter extends PagedQuery {
  status?: BranchStatus | '';
}

export interface BranchRequest {
  branchCode: string;
  name: string;
  address: string;
  phone?: string | null;
  email?: string | null;
  latitude: number;
  longitude: number;
  radius: number;
  status?: BranchStatus;
}

export interface BranchWifiRequest {
  ssid: string;
  bssid: string;
  isActive: boolean;
}

export interface Department {
  id: number;
  code: string;
  name: string;
  description: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface DepartmentRequest {
  code: string;
  name: string;
  description?: string | null;
}

export interface Position {
  id: number;
  title: string;
  baseSalaryPerHour: number;
  description: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface PositionRequest {
  title: string;
  baseSalaryPerHour: number;
  description?: string | null;
}
