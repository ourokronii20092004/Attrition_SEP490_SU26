import { apiFetch } from "./client";
import type { ApiResponse, NotificationDto } from "../types";

export const notificationsApi = {
  list: (limit = 20) =>
    apiFetch<ApiResponse<NotificationDto[]>>(`/api/notifications?limit=${limit}`),

  unreadCount: () =>
    apiFetch<ApiResponse<number>>("/api/notifications/unread-count"),

  markRead: (id: string) =>
    apiFetch<ApiResponse<void>>(`/api/notifications/${id}/read`, { method: "PUT" }),

  markAllRead: () =>
    apiFetch<ApiResponse<void>>("/api/notifications/read-all", { method: "PUT" }),
};
