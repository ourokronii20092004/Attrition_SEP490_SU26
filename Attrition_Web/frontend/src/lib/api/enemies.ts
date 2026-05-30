import { apiFetch } from "./client";
import type { ApiResponse, EnemyResponse, EnemyCreateRequest, EnemyUpdateRequest } from "../types";

export const enemiesApi = {
  list: (params?: { tier?: string; search?: string }) => {
    const sp = new URLSearchParams();
    if (params?.tier) sp.set("tier", params.tier);
    if (params?.search) sp.set("search", params.search);
    const qs = sp.toString();
    return apiFetch<ApiResponse<EnemyResponse[]>>(`/api/enemies${qs ? `?${qs}` : ""}`, { auth: false });
  },

  get: (id: string) =>
    apiFetch<ApiResponse<EnemyResponse>>(`/api/enemies/${id}`, { auth: false }),

  create: (data: EnemyCreateRequest) =>
    apiFetch<ApiResponse<EnemyResponse>>("/api/enemies", { method: "POST", body: data }),

  update: (id: string, data: EnemyUpdateRequest) =>
    apiFetch<ApiResponse<EnemyResponse>>(`/api/enemies/${id}`, { method: "PUT", body: data }),

  delete: (id: string) =>
    apiFetch<ApiResponse<void>>(`/api/enemies/${id}`, { method: "DELETE" }),
};
