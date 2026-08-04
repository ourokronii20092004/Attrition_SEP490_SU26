import { apiFetch } from "./client";
import type {
  ApiResponse,
  PaginatedResponse,
  CharacterSummaryDto,
  CharacterDetailDto,
  AdminCharacterDto,
  SessionStatusDto,
  SaveListDto,
  SaveDetailDto,
  DeleteSaveResultDto,
  AdminRoomListDto,
  AdminRoomDetailDto,
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

  // ── Save files ─────────────────────────────────────────────────────────────
  // Paged history for one character, newest first.
  getSaves: (characterId: string, params?: { page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<ApiResponse<SaveListDto>>(
      `/api/characters/${characterId}/saves${qs ? `?${qs}` : ""}`);
  },

  // One save in full — every number as it was at that moment.
  getSave: (characterId: string, saveId: number) =>
    apiFetch<ApiResponse<SaveDetailDto>>(`/api/characters/${characterId}/saves/${saveId}`),

  /**
   * Delete a save. Deleting the newest rolls live game state back to the previous save;
   * `alsoRollBackWorldState` additionally restores the room's shared progress, which the server
   * honours only for the room's owner.
   */
  deleteSave: (characterId: string, saveId: number, alsoRollBackWorldState = false) =>
    apiFetch<ApiResponse<DeleteSaveResultDto>>(
      `/api/characters/${characterId}/saves/${saveId}`,
      { method: "DELETE", body: { alsoRollBackWorldState } }),

  // ── Admin: co-op rooms ─────────────────────────────────────────────────────
  getAdminRooms: (params?: { page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<ApiResponse<AdminRoomListDto>>(`/api/admin/rooms${qs ? `?${qs}` : ""}`);
  },

  getAdminRoom: (id: string) =>
    apiFetch<ApiResponse<AdminRoomDetailDto>>(`/api/admin/rooms/${id}`),
};