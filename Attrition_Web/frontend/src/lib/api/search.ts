import { apiFetch } from "./client";
import type { ApiResponse, GlobalSearchResponse, SearchSuggestionDto } from "../types";

export const searchApi = {
  search: (q: string, limit = 5) =>
    apiFetch<ApiResponse<GlobalSearchResponse>>(`/api/search?q=${encodeURIComponent(q)}&limit=${limit}`, { auth: false }),

  suggest: (q: string, limit = 6) =>
    apiFetch<ApiResponse<SearchSuggestionDto[]>>(`/api/search/suggest?q=${encodeURIComponent(q)}&limit=${limit}`, { auth: false }),
};
