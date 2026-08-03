import { apiFetch } from "./client";
import type { ApiResponse, NotificationDto, PaginatedResponse } from "../types";

export const notificationsApi = {
  list: (limit = 20) =>
    apiFetch<ApiResponse<NotificationDto[]>>(`/api/notifications?limit=${limit}`),

  listPaged: (params?: { page?: number; pageSize?: number; unreadOnly?: boolean }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    if (params?.unreadOnly) sp.set("unreadOnly", "true");
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<NotificationDto>>>(`/api/notifications/paged${qs ? `?${qs}` : ""}`);
  },

  unreadCount: () =>
    apiFetch<ApiResponse<number>>("/api/notifications/unread-count"),

  markRead: (id: string) =>
    apiFetch<ApiResponse<void>>(`/api/notifications/${id}/read`, { method: "PUT" }),

  markAllRead: () =>
    apiFetch<ApiResponse<void>>("/api/notifications/read-all", { method: "PUT" }),

  /** Clears this user's unread notifications for one thread. Returns how many were cleared. */
  markThreadRead: (threadId: string) =>
    apiFetch<ApiResponse<number>>(`/api/notifications/thread/${threadId}/read`, { method: "PUT" }),
};
