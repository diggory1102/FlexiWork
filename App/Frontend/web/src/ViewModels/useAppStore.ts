import { create } from 'zustand';
import { User, Branch } from '../Models';

interface AppState {
  currentUser: User | null;
  currentBranch: Branch | null;
  role: 'ADMIN' | 'MANAGER' | 'EMPLOYEE';
  setCurrentUser: (user: User | null) => void;
  setCurrentBranch: (branch: Branch | null) => void;
  setRole: (role: 'ADMIN' | 'MANAGER' | 'EMPLOYEE') => void;
}

export const useAppStore = create<AppState>((set) => ({
  currentUser: {
    id: 3,
    employeeCode: 'EMP003',
    email: 'nhanvien.cg@flexiwork.vn',
    fullName: 'Lê Văn Nhân Viên',
    status: 'active',
    branchId: 1,
  },
  currentBranch: {
    id: 1,
    branchCode: 'CN-HN-01',
    name: 'FlexiWork Cầu Giấy',
    address: 'Tòa nhà FPT Cầu Giấy, Duy Tân, Hà Nội',
    latitude: 21.031584,
    longitude: 105.783115,
    radius: 50,
    status: 'active',
  },
  role: 'EMPLOYEE',
  setCurrentUser: (user) => set({ currentUser: user }),
  setCurrentBranch: (branch) => set({ currentBranch: branch }),
  setRole: (role) => set({ role }),
}));
