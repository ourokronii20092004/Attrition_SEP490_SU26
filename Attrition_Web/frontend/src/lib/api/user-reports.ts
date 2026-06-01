import { apiFetch } from "./client";
import type { ApiResponse, PaginatedResponse, AdminUserReportDto } from "../types";

export const userReportsApi = {
  // QOLF-9: report another user; admins review before acting.
  report: (userId: string, reason: string) =>
    apiFetch<ApiResponse<void>>(`/api/users/${userId}/report`, { method: "POST", body: { reason } }),

  // Admin moderation queue
  adminList: (params?: { status?: string; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.status) sp.set("status", params.status);
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<AdminUserReportDto>>>(`/api/admin/user-reports${qs ? `?${qs}` : ""}`);
  },

  adminResolve: (id: string) =>
    apiFetch<ApiResponse<void>>(`/api/admin/user-reports/${id}/resolve`, { method: "PUT" }),

  adminDismiss: (id: string) =>
    apiFetch<ApiResponse<void>>(`/api/admin/user-reports/${id}/dismiss`, { method: "PUT" }),
};
