import { apiFetch } from "./client";
import type { ApiResponse, PaginatedResponse, AdminStatsDto, UserListItem, AdminUserDetailDto } from "../types";

export const adminApi = {
  getStats: () =>
    apiFetch<ApiResponse<AdminStatsDto>>("/api/admin/stats"),

  // User management
  getUsers: (params?: { page?: number; pageSize?: number; search?: string; sort?: string }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    if (params?.search) sp.set("search", params.search);
    if (params?.sort) sp.set("sort", params.sort);
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<UserListItem>>>(`/api/admin/users${qs ? `?${qs}` : ""}`);
  },

  getUserDetail: (userId: string) =>
    apiFetch<ApiResponse<AdminUserDetailDto>>(`/api/admin/users/${userId}`),

  setUserRole: (userId: string, role: string) =>
    apiFetch<ApiResponse<void>>(`/api/admin/users/${userId}/role`, {
      method: "PUT",
      body: { role },
    }),

  toggleBan: (userId: string) =>
    apiFetch<ApiResponse<void>>(`/api/admin/users/${userId}/ban`, { method: "POST" }),

  resetUserPassword: (userId: string, newPassword: string) =>
    apiFetch<ApiResponse<void>>(`/api/admin/users/${userId}/reset-password`, {
      method: "PUT",
      body: { newPassword },
    }),

  deleteUser: (userId: string) =>
    apiFetch<ApiResponse<void>>(`/api/admin/users/${userId}`, { method: "DELETE" }),
};
