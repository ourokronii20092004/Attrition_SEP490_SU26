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

  deleteAccount: () =>
    apiFetch<ApiResponse<void>>("/api/account/me", { method: "DELETE" }),
};
