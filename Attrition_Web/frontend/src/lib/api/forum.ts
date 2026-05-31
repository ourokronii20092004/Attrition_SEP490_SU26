import { apiFetch } from "./client";
import type {
  ApiResponse,
  PaginatedResponse,
  ForumCategoryDto,
  ForumThreadDto,
  ForumThreadListDto,
  ForumPostDto,
  AdminForumThreadDto,
  AdminForumPostDto,
  AdminPostReportDto,
  CreateThreadRequest,
  CreatePostRequest,
  UpdatePostRequest,
  ReactRequest,
  ReportPostReq,
  RemovePostRequest,
  ForumCategoryRequest,
} from "../types";

export const forumApi = {
  getCategories: () =>
    apiFetch<ApiResponse<ForumCategoryDto[]>>("/api/forum/categories", { auth: false }),

  getThreads: (params?: { categoryId?: number; authorId?: string; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.categoryId != null) sp.set("categoryId", String(params.categoryId));
    if (params?.authorId) sp.set("authorId", params.authorId);
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<ForumThreadListDto>>>(`/api/forum/threads${qs ? `?${qs}` : ""}`, { auth: false });
  },

  getThread: (id: string) =>
    apiFetch<ApiResponse<ForumThreadDto>>(`/api/forum/threads/${id}`, { auth: false }),

  getPosts: (threadId: string, params?: { page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<ForumPostDto>>>(`/api/forum/threads/${threadId}/posts${qs ? `?${qs}` : ""}`);
  },

  createThread: (data: CreateThreadRequest) =>
    apiFetch<ApiResponse<ForumThreadDto>>("/api/forum/threads", { method: "POST", body: data }),

  createPost: (threadId: string, data: CreatePostRequest) =>
    apiFetch<ApiResponse<ForumPostDto>>(`/api/forum/threads/${threadId}/posts`, { method: "POST", body: data }),

  updatePost: (postId: string, data: UpdatePostRequest) =>
    apiFetch<ApiResponse<ForumPostDto>>(`/api/forum/posts/${postId}`, { method: "PUT", body: data }),

  deletePost: (postId: string) =>
    apiFetch<ApiResponse<void>>(`/api/forum/posts/${postId}`, { method: "DELETE" }),

  react: (postId: string, data: ReactRequest) =>
    apiFetch<ApiResponse<void>>(`/api/forum/posts/${postId}/react`, { method: "POST", body: data }),

  report: (postId: string, data: ReportPostReq) =>
    apiFetch<ApiResponse<void>>(`/api/forum/posts/${postId}/report`, { method: "POST", body: data }),

  // Admin
  pinThread: (threadId: string) =>
    apiFetch<ApiResponse<void>>(`/api/forum/threads/${threadId}/pin`, { method: "PUT" }),

  lockThread: (threadId: string) =>
    apiFetch<ApiResponse<void>>(`/api/forum/threads/${threadId}/lock`, { method: "PUT" }),

  deleteThread: (threadId: string) =>
    apiFetch<ApiResponse<void>>(`/api/forum/threads/${threadId}`, { method: "DELETE" }),

  createCategory: (data: ForumCategoryRequest) =>
    apiFetch<ApiResponse<ForumCategoryDto>>("/api/forum/categories", { method: "POST", body: data }),

  updateCategory: (id: number, data: ForumCategoryRequest) =>
    apiFetch<ApiResponse<ForumCategoryDto>>(`/api/forum/categories/${id}`, { method: "PUT", body: data }),

  deleteCategory: (id: number) =>
    apiFetch<ApiResponse<void>>(`/api/forum/categories/${id}`, { method: "DELETE" }),

  // Admin moderation (paginated)
  getAdminThreads: (params?: { page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<AdminForumThreadDto>>>(`/api/admin/forum/threads${qs ? `?${qs}` : ""}`);
  },

  getAdminPosts: (params?: { removedOnly?: boolean; search?: string; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.removedOnly) sp.set("removedOnly", "true");
    if (params?.search) sp.set("search", params.search);
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<AdminForumPostDto>>>(`/api/admin/forum/posts${qs ? `?${qs}` : ""}`);
  },

  removePost: (postId: string, data: RemovePostRequest) =>
    apiFetch<ApiResponse<void>>(`/api/admin/forum/posts/${postId}/remove`, { method: "POST", body: data }),

  restorePost: (postId: string) =>
    apiFetch<ApiResponse<void>>(`/api/admin/forum/posts/${postId}/restore`, { method: "POST" }),

  getReports: (params?: { status?: string; page?: number; pageSize?: number }) => {
    const sp = new URLSearchParams();
    if (params?.status) sp.set("status", params.status);
    if (params?.page) sp.set("page", String(params.page));
    if (params?.pageSize) sp.set("pageSize", String(params.pageSize));
    const qs = sp.toString();
    return apiFetch<ApiResponse<PaginatedResponse<AdminPostReportDto>>>(`/api/admin/forum/reports${qs ? `?${qs}` : ""}`);
  },

  dismissReport: (reportId: string) =>
    apiFetch<ApiResponse<void>>(`/api/admin/forum/reports/${reportId}/dismiss`, { method: "PUT" }),

  resolveReport: (reportId: string) =>
    apiFetch<ApiResponse<void>>(`/api/admin/forum/reports/${reportId}/resolve`, { method: "PUT" }),
};
