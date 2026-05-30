import { apiFetch } from "./client";
import type {
  ApiResponse,
  PaginatedResponse,
  WikiCategoryDto,
  WikiArticleDto,
  WikiArticleListDto,
  WikiRevisionDto,
  WikiContributionDto,
  CreateArticleRequest,
  UpdateArticleRequest,
  SuggestEditRequest,
  ReviewContributionRequest,
  WikiCategoryRequest,
} from "../types";

export const wikiApi = {
  getCategories: () =>
    apiFetch<ApiResponse<WikiCategoryDto[]>>("/api/wiki/categories", { auth: false }),

  getArticles: (params?: { category?: string; search?: string; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.category) sp.set("category", params.category);
    if (params?.search) sp.set("search", params.search);
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<WikiArticleListDto>>>(`/api/wiki/articles${qs ? `?${qs}` : ""}`, { auth: false });
  },

  getArticle: (slug: string) =>
    apiFetch<ApiResponse<WikiArticleDto>>(`/api/wiki/articles/${encodeURIComponent(slug)}`, { auth: false }),

  getRevisions: (articleId: string) =>
    apiFetch<ApiResponse<WikiRevisionDto[]>>(`/api/wiki/articles/${articleId}/revisions`, { auth: false }),

  getRevision: (articleId: string, revisionId: string) =>
    apiFetch<ApiResponse<WikiRevisionDto>>(`/api/wiki/articles/${articleId}/revisions/${revisionId}`, { auth: false }),

  suggestEdit: (articleId: string, data: SuggestEditRequest) =>
    apiFetch<ApiResponse<void>>(`/api/wiki/articles/${articleId}/suggest`, { method: "POST", body: data }),

  createArticle: (data: CreateArticleRequest) =>
    apiFetch<ApiResponse<WikiArticleDto>>("/api/wiki/articles", { method: "POST", body: data }),

  updateArticle: (id: string, data: UpdateArticleRequest) =>
    apiFetch<ApiResponse<WikiArticleDto>>(`/api/wiki/articles/${id}`, { method: "PUT", body: data }),

  deleteArticle: (id: string) =>
    apiFetch<ApiResponse<void>>(`/api/wiki/articles/${id}`, { method: "DELETE" }),

  getContributions: () =>
    apiFetch<ApiResponse<WikiContributionDto[]>>("/api/wiki/contributions"),

  reviewContribution: (id: string, data: ReviewContributionRequest) =>
    apiFetch<ApiResponse<void>>(`/api/wiki/contributions/${id}/review`, { method: "POST", body: data }),

  createCategory: (data: WikiCategoryRequest) =>
    apiFetch<ApiResponse<WikiCategoryDto>>("/api/wiki/categories", { method: "POST", body: data }),

  updateCategory: (id: number, data: WikiCategoryRequest) =>
    apiFetch<ApiResponse<WikiCategoryDto>>(`/api/wiki/categories/${id}`, { method: "PUT", body: data }),

  deleteCategory: (id: number) =>
    apiFetch<ApiResponse<void>>(`/api/wiki/categories/${id}`, { method: "DELETE" }),
};
