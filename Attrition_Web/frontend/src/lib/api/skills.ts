import { apiFetch } from "./client";
import type { ApiResponse, SkillResponse, SkillUpdateRequest } from "../types";

export const skillsApi = {
  list: () => apiFetch<ApiResponse<SkillResponse[]>>("/api/skills", { auth: false }),
  get: (id: string) => apiFetch<ApiResponse<SkillResponse>>(`/api/skills/${encodeURIComponent(id)}`, { auth: false }),
  update: (id: string, data: SkillUpdateRequest) =>
    apiFetch<ApiResponse<SkillResponse>>(`/api/skills/${id}`, { method: "PUT", body: data }),
};
