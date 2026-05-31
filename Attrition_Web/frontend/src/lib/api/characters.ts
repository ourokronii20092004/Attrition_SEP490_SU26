import { apiFetch } from "./client";
import type {
  ApiResponse,
  PaginatedResponse,
  CharacterSummaryDto,
  CharacterDetailDto,
  AdminCharacterDto,
  SessionStatusDto,
} from "../types";

export const charactersApi = {
  // Player: my own characters (read-only)
  getMine: () =>
    apiFetch<ApiResponse<CharacterSummaryDto[]>>("/api/characters"),

  get: (id: string) =>
    apiFetch<ApiResponse<CharacterDetailDto>>(`/api/characters/${id}`),

  // Admin: every player's characters (paginated)
  getAll: (params?: { page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<AdminCharacterDto>>>(`/api/admin/characters${qs ? `?${qs}` : ""}`);
  },

  getAdmin: (id: string) =>
    apiFetch<ApiResponse<CharacterDetailDto>>(`/api/admin/characters/${id}`),

  // Ban/liveness poll — returns 403 (thrown as ApiError) when the account is banned.
  sessionCheck: () =>
    apiFetch<ApiResponse<SessionStatusDto>>("/api/auth/session-check"),
};
