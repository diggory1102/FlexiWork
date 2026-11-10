/** Khớp với FW.Application.Common.Models.ApiResponse<T> ở backend. */
export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T | null;
  errors: string[] | null;
  timestamp: string;
}

/** Khớp với FW.Application.Common.Models.PaginatedResult<T> ở backend. */
export interface PaginatedResult<T> {
  items: T[];
  pageIndex: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

/** Tham số phân trang & tìm kiếm (PagedQuery ở backend). */
export interface PagedQuery {
  pageIndex?: number;
  pageSize?: number;
  search?: string;
}
