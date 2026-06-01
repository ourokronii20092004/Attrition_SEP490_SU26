import { apiFetch } from "./client";
import type {
  ApiResponse,
  UserDto,
  UpdateProfileRequest,
  UpdateThemeRequest,
  UpdateEmailRequest,
  SetPasswordRequest,
} from "../types";

export const accountApi = {
  getProfile: (username: string) =>
    apiFetch<ApiResponse<UserDto>>(`/api/account/profile/${encodeURIComponent(username)}`, { auth: false }),

  updateProfile: (data: UpdateProfileRequest) =>
    apiFetch<ApiResponse<UserDto>>("/api/account/profile", { method: "PUT", body: data }),

  updateTheme: (data: UpdateThemeRequest) =>
    apiFetch<ApiResponse<void>>("/api/account/theme", { method: "PUT", body: data }),

  uploadAvatar: (file: File) => {
    const form = new FormData();
    form.append("file", file);
    return apiFetch<ApiResponse<string>>("/api/account/avatar", { method: "POST", body: form });
  },

  deleteAvatar: () =>
    apiFetch<ApiResponse<void>>("/api/account/avatar", { method: "DELETE" }),

  uploadBackground: (file: File) => {
    const form = new FormData();
    form.append("file", file);
    return apiFetch<ApiResponse<string>>("/api/account/background", { method: "POST", body: form });
  },

  deleteBackground: () =>
    apiFetch<ApiResponse<void>>("/api/account/background", { method: "DELETE" }),

  setPassword: (data: SetPasswordRequest) =>
    apiFetch<ApiResponse<void>>("/api/account/set-password", { method: "POST", body: data }),

  updateEmail: (data: UpdateEmailRequest) =>
    apiFetch<ApiResponse<void>>("/api/account/email", { method: "PUT", body: data }),

  // Account deletion (PROB-4): request emails a confirmation link; confirm soft-deletes with a
  // 90-day recovery window (sign back in within 90 days to restore).
  requestDeletion: () =>
    apiFetch<ApiResponse<void>>("/api/account/request-deletion", { method: "POST" }),

  confirmDeletion: (token: string) =>
    apiFetch<ApiResponse<void>>("/api/account/confirm-deletion", { method: "POST", body: { token } }),
};
