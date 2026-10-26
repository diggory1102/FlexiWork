import { create } from 'zustand';
import { EmployeeUser, Branch } from '../types';

interface AuthSessionState {
  user: EmployeeUser | null;
  branch: Branch | null;
  token: string | null;
  isAuthenticated: boolean;
  setUser: (user: EmployeeUser | null) => void;
  setBranch: (branch: Branch | null) => void;
  setToken: (token: string | null) => void;
  logout: () => void;
}

export const useAuthStore = create<AuthSessionState>((set) => ({
  user: {
    id: 3,
    employeeCode: 'EMP003',
    email: 'nhanvien.cg@flexiwork.vn',
    fullName: 'Lê Văn Nhân Viên',
    status: 'active',
    branchId: 1,
  },
  branch: {
    id: 1,
    branchCode: 'CN-HN-01',
    name: 'FlexiWork Cầu Giấy',
    address: 'Tòa nhà FPT Cầu Giấy, Duy Tân, Hà Nội',
    latitude: 21.031584,
    longitude: 105.783115,
    radius: 50,
    status: 'active',
  },
  token: 'mock-jwt-token-emp003',
  isAuthenticated: true,
  setUser: (user) => set({ user, isAuthenticated: !!user }),
  setBranch: (branch) => set({ branch }),
  setToken: (token) => set({ token }),
  logout: () => set({ user: null, branch: null, token: null, isAuthenticated: false }),
}));
