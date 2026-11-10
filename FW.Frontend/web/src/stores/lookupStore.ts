import { create } from 'zustand';
import { branchApi, departmentApi, positionApi } from '@/services/organizationApi';
import type { Branch, Department, Position } from '@/types/organization';

interface LookupState {
  branches: Branch[];
  departments: Department[];
  positions: Position[];
  loaded: boolean;
  /** Nạp danh mục dùng cho các ô chọn (chỉ gọi API lần đầu, trừ khi force). */
  load: (force?: boolean) => Promise<void>;
  invalidate: () => void;
}

/** Danh mục nhỏ (chi nhánh, phòng ban, chức vụ) dùng chung cho dropdown & bộ lọc. */
export const useLookupStore = create<LookupState>((set, get) => ({
  branches: [],
  departments: [],
  positions: [],
  loaded: false,
  load: async (force = false) => {
    if (get().loaded && !force) return;
    const [branches, departments, positions] = await Promise.all([
      branchApi.list({ pageSize: 100 }),
      departmentApi.list({ pageSize: 100 }),
      positionApi.list({ pageSize: 100 }),
    ]);
    set({
      branches: branches.data.items,
      departments: departments.data.items,
      positions: positions.data.items,
      loaded: true,
    });
  },
  invalidate: () => set({ loaded: false }),
}));
