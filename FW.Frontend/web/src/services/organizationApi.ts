import { api } from './apiClient';
import type { PagedQuery, PaginatedResult } from '@/types/api';
import type {
  AddressLookup,
  Branch,
  BranchDetail,
  BranchFilter,
  BranchRequest,
  BranchWifi,
  BranchWifiRequest,
  Department,
  DepartmentRequest,
  Position,
  PositionRequest,
} from '@/types/organization';

export const branchApi = {
  list: (filter: BranchFilter) => api.get<PaginatedResult<Branch>>('/branches', filter),
  get: (id: number) => api.get<BranchDetail>(`/branches/${id}`),
  create: (body: BranchRequest) => api.post<Branch>('/branches', body),
  update: (id: number, body: BranchRequest) => api.put<Branch>(`/branches/${id}`, body),
  remove: (id: number) => api.delete<null>(`/branches/${id}`),
  /** Tìm tọa độ từ địa chỉ (OpenStreetMap) để điền tâm Geofence. */
  geocode: (address: string) => api.get<AddressLookup>('/branches/geocode', { address }),

  addWifi: (branchId: number, body: BranchWifiRequest) => api.post<BranchWifi>(`/branches/${branchId}/wifis`, body),
  updateWifi: (branchId: number, wifiId: number, body: BranchWifiRequest) =>
    api.put<BranchWifi>(`/branches/${branchId}/wifis/${wifiId}`, body),
  removeWifi: (branchId: number, wifiId: number) => api.delete<null>(`/branches/${branchId}/wifis/${wifiId}`),
};

export const departmentApi = {
  list: (query: PagedQuery) => api.get<PaginatedResult<Department>>('/departments', query),
  create: (body: DepartmentRequest) => api.post<Department>('/departments', body),
  update: (id: number, body: DepartmentRequest) => api.put<Department>(`/departments/${id}`, body),
  remove: (id: number) => api.delete<null>(`/departments/${id}`),
};

export const positionApi = {
  list: (query: PagedQuery) => api.get<PaginatedResult<Position>>('/positions', query),
  create: (body: PositionRequest) => api.post<Position>('/positions', body),
  update: (id: number, body: PositionRequest) => api.put<Position>(`/positions/${id}`, body),
  remove: (id: number) => api.delete<null>(`/positions/${id}`),
};
