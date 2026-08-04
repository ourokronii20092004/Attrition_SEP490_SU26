import { apiFetch } from "./client";
import type { ApiResponse, SessionSummaryDto, SessionDetailDto } from "../types";

/**
 * Co-op rooms ("sessions"). Read-only from the web — the Unity host is the source of truth and
 * pushes the whole party's progress via POST /api/sessions/bulk. Both endpoints are scoped to the
 * caller by the server (a room you don't own returns 403), so there is no owner param here.
 */
export const sessionsApi = {
  /** Rooms hosted by the signed-in user, newest-played first. */
  getMine: () => apiFetch<ApiResponse<SessionSummaryDto[]>>("/api/sessions"),

  /** Full room load: every character's progress plus world state and fog. */
  get: (id: string) => apiFetch<ApiResponse<SessionDetailDto>>(`/api/sessions/${id}`),
};
