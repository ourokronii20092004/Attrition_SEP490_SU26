import { apiFetch } from "./client";
import type { ApiResponse, PaginatedResponse, AssetDto, UpdateAssetReq } from "../types";

export const assetsApi = {
  list: (params?: { page?: number; pageSize?: number; assetType?: string }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    if (params?.assetType) sp.set("assetType", params.assetType);
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<AssetDto>>>(`/api/assets${qs ? `?${qs}` : ""}`, { auth: false });
  },

  get: (id: string) =>
    apiFetch<ApiResponse<AssetDto>>(`/api/assets/${id}`, { auth: false }),

  // Admin
  adminList: (params?: { page?: number; pageSize?: number; assetType?: string; search?: string }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    if (params?.assetType) sp.set("assetType", params.assetType);
    if (params?.search) sp.set("search", params.search);
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<AssetDto>>>(`/api/admin/assets${qs ? `?${qs}` : ""}`);
  },

  create: (file: File, data: { assetType: string; title?: string; description?: string; tags?: string }) => {
    const form = new FormData();
    form.append("file", file);
    form.append("assetType", data.assetType);
    if (data.title) form.append("title", data.title);
    if (data.description) form.append("description", data.description);
    if (data.tags) form.append("tags", data.tags);
    return apiFetch<ApiResponse<AssetDto>>("/api/admin/assets", { method: "POST", body: form });
  },

  update: (id: string, data: UpdateAssetReq) =>
    apiFetch<ApiResponse<AssetDto>>(`/api/admin/assets/${id}`, { method: "PUT", body: data }),

  delete: (id: string) =>
    apiFetch<ApiResponse<void>>(`/api/admin/assets/${id}`, { method: "DELETE" }),

  // Inline image upload for any logged-in user (e.g. forum post images). Returns the public URL.
  uploadInlineImage: (file: File) => {
    const form = new FormData();
    form.append("file", file);
    return apiFetch<ApiResponse<string>>("/api/assets/inline-image", { method: "POST", body: form });
  },
};
