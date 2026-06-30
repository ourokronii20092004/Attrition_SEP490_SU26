import { apiFetch } from "./client";
import type { ApiResponse, ItemResponse, ItemCreateRequest, ItemUpdateRequest } from "../types";

export const itemsApi = {
  list: (params?: { category?: string; search?: string }) => {
    const sp = new URLSearchParams();
    if (params?.category) sp.set("category", params.category);
    if (params?.search) sp.set("search", params.search);
    const qs = sp.toString();
    return apiFetch<ApiResponse<ItemResponse[]>>(`/api/items${qs ? `?${qs}` : ""}`, { auth: false });
  },

  get: (id: string) =>
    apiFetch<ApiResponse<ItemResponse>>(`/api/items/${id}`, { auth: false }),

  create: (data: ItemCreateRequest) =>
    apiFetch<ApiResponse<ItemResponse>>("/api/items", { method: "POST", body: data }),

  update: (id: string, data: ItemUpdateRequest) =>
    apiFetch<ApiResponse<ItemResponse>>(`/api/items/${id}`, { method: "PUT", body: data }),

  delete: (id: string) =>
    apiFetch<ApiResponse<void>>(`/api/items/${id}`, { method: "DELETE" }),
};
