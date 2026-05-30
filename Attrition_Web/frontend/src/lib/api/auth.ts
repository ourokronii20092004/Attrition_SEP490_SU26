import { apiFetch } from "./client";
import type {
  ApiResponse,
  AuthResponse,
  LoginRequest,
  RegisterRequest,
  GoogleAuthRequest,
  RefreshRequest,
  ChangePasswordRequest,
  ForgotPasswordRequest,
  ResetPasswordRequest,
  VerifyEmailRequest,
  UserDto,
} from "../types";

export const authApi = {
  login: (data: LoginRequest) =>
    apiFetch<ApiResponse<AuthResponse>>("/api/auth/login", { method: "POST", body: data }),

  register: (data: RegisterRequest) =>
    apiFetch<ApiResponse<AuthResponse>>("/api/auth/register", { method: "POST", body: data }),

  google: (data: GoogleAuthRequest) =>
    apiFetch<ApiResponse<AuthResponse>>("/api/auth/google", { method: "POST", body: data }),

  googleLink: (data: GoogleAuthRequest) =>
    apiFetch<ApiResponse<void>>("/api/auth/google/link", { method: "POST", body: data }),

  googleUnlink: () =>
    apiFetch<ApiResponse<void>>("/api/auth/google/unlink", { method: "POST" }),

  refresh: (data: RefreshRequest) =>
    apiFetch<ApiResponse<AuthResponse>>("/api/auth/refresh", { method: "POST", body: data, auth: false }),

  me: () =>
    apiFetch<ApiResponse<UserDto>>("/api/auth/me"),

  changePassword: (data: ChangePasswordRequest) =>
    apiFetch<ApiResponse<void>>("/api/auth/change-password", { method: "POST", body: data }),

  logout: () =>
    apiFetch<ApiResponse<void>>("/api/auth/logout", { method: "POST" }),

  forgotPassword: (data: ForgotPasswordRequest) =>
    apiFetch<ApiResponse<void>>("/api/auth/forgot-password", { method: "POST", body: data, auth: false }),

  resetPassword: (data: ResetPasswordRequest) =>
    apiFetch<ApiResponse<void>>("/api/auth/reset-password", { method: "POST", body: data, auth: false }),

  verifyEmail: (data: VerifyEmailRequest) =>
    apiFetch<ApiResponse<void>>("/api/auth/verify-email", { method: "POST", body: data, auth: false }),

  resendVerification: () =>
    apiFetch<ApiResponse<void>>("/api/auth/verify-email/resend", { method: "POST" }),
};
