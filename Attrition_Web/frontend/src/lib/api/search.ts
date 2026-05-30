import { apiFetch } from "./client";
import type { ApiResponse, GlobalSearchResponse } from "../types";

export const searchApi = {
  search: (q: string, limit = 5) =>
    apiFetch<ApiResponse<GlobalSearchResponse>>(`/api/search?q=${encodeURIComponent(q)}&limit=${limit}`, { auth: false }),
};
