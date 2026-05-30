import { apiFetch } from "./client";
import type {
  ApiResponse,
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

  // Admin: every player's characters
  getAll: () =>
    apiFetch<ApiResponse<AdminCharacterDto[]>>("/api/admin/characters"),

  getAdmin: (id: string) =>
    apiFetch<ApiResponse<CharacterDetailDto>>(`/api/admin/characters/${id}`),

  // Ban/liveness poll — returns 403 (thrown as ApiError) when the account is banned.
  sessionCheck: () =>
    apiFetch<ApiResponse<SessionStatusDto>>("/api/auth/session-check"),
};
