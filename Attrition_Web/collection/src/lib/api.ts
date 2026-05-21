/**
 * API Client — Production-grade fetch wrapper
 *
 * Features:
 * - JWT auto-attach from localStorage
 * - Token refresh queue (single refresh for concurrent 401s)
 * - Request timeout handling
 * - Error normalization
 * - Server-side aware (skips auth in Server Components)
 * - Multipart upload support
 */

const API_BASE =
  typeof window !== "undefined"
    ? (process.env.NEXT_PUBLIC_API_URL || "") + "/api"
    : (process.env.API_URL || "http://localhost:5000") + "/api";
const TIMEOUT_MS = 15000;
const TOKEN_KEY = "attrition-token";
const REFRESH_KEY = "attrition-refresh";

// ─── Types ─────────────────────────────────────────────────

export interface ApiResponse<T = unknown> {
  success: boolean;
  data?: T;
  error?: string;
  // Pagination fields (returned as siblings of data)
  totalCount?: number;
  page?: number;
  pageSize?: number;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// ─── Token Management ──────────────────────────────────────

const isClient = typeof window !== "undefined";

export function getCookie(name: string): string | null {
  if (!isClient) return null;
  const match = document.cookie.match(new RegExp('(^| )' + name + '=([^;]+)'));
  return match ? match[2] : null;
}

export function setCookie(name: string, value: string, maxAge: number) {
  if (!isClient) return;
  const domain = window.location.hostname.includes("hault.io.vn") ? "domain=.hault.io.vn;" : "";
  document.cookie = `${name}=${value}; ${domain} path=/; max-age=${maxAge}; SameSite=Lax`;
}

export function deleteCookie(name: string) {
  if (!isClient) return;
  const domain = window.location.hostname.includes("hault.io.vn") ? "domain=.hault.io.vn;" : "";
  document.cookie = `${name}=; ${domain} path=/; expires=Thu, 01 Jan 1970 00:00:00 GMT`;
}

export function getAccessToken(): string | null {
  if (!isClient) return null;
  const authState = getCookie("attrition-auth-state");
  if (authState === "logged-out") {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    return null;
  }
  
  const cookieToken = getCookie(TOKEN_KEY);
  if (cookieToken) {
    localStorage.setItem(TOKEN_KEY, cookieToken);
    const cookieRefresh = getCookie(REFRESH_KEY);
    if (cookieRefresh) localStorage.setItem(REFRESH_KEY, cookieRefresh);
    return cookieToken;
  }
  
  return localStorage.getItem(TOKEN_KEY);
}

export function getRefreshToken(): string | null {
  if (!isClient) return null;
  return getCookie(REFRESH_KEY) || localStorage.getItem(REFRESH_KEY);
}

export function setTokens(accessToken: string, refreshToken: string): void {
  if (!isClient) return;
  localStorage.setItem(TOKEN_KEY, accessToken);
  localStorage.setItem(REFRESH_KEY, refreshToken);
  setCookie(TOKEN_KEY, accessToken, 86400); // 1 day
  setCookie(REFRESH_KEY, refreshToken, 604800); // 7 days
  setCookie("attrition-auth-state", "logged-in", 604800);
}

export function clearTokens(): void {
  if (!isClient) return;
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(REFRESH_KEY);
  deleteCookie(TOKEN_KEY);
  deleteCookie(REFRESH_KEY);
  setCookie("attrition-auth-state", "logged-out", 86400);
}

// ─── Refresh Queue ─────────────────────────────────────────
// If multiple requests hit 401 simultaneously, only ONE refresh
// fires. Others queue and retry with the new token.

let refreshPromise: Promise<string | null> | null = null;

async function refreshAccessToken(): Promise<string | null> {
  const refreshToken = getRefreshToken();
  if (!refreshToken) return null;

  try {
    const res = await fetch(`${API_BASE}/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    });

    if (!res.ok) {
      clearTokens();
      return null;
    }

    const data = await res.json();
    if (data.success && data.data) {
      setTokens(data.data.accessToken, data.data.refreshToken);
      return data.data.accessToken;
    }

    clearTokens();
    return null;
  } catch {
    clearTokens();
    return null;
  }
}

async function getRefreshedToken(): Promise<string | null> {
  if (!refreshPromise) {
    refreshPromise = refreshAccessToken().finally(() => {
      refreshPromise = null;
    });
  }
  return refreshPromise;
}

// ─── Core Fetch ────────────────────────────────────────────

interface FetchOptions {
  method?: string;
  body?: unknown;
  headers?: Record<string, string>;
  timeout?: number;
  skipAuth?: boolean;
}

class ApiError extends Error {
  constructor(
    public status: number,
    message: string
  ) {
    super(message);
    this.name = "ApiError";
  }
}

/**
 * Extract a human-readable error message from various API response formats:
 * - Attrition API:       { success: false, error: "..." }
 * - ASP.NET validation:  { errors: { Field: ["msg"] } }
 * - RFC 7807:            { title: "...", detail: "..." }
 * - FluentValidation:    { errors: [{ errorMessage: "..." }] }
 */
function parseErrorBody(data: Record<string, unknown>, status: number): string {
  // Our API format
  if (typeof data.error === "string" && data.error) return data.error;

  // ASP.NET model validation: { errors: { Password: ["Must contain..."] } }
  if (data.errors && typeof data.errors === "object") {
    const errors = data.errors as Record<string, string[]>;
    const messages: string[] = [];
    for (const field of Object.keys(errors)) {
      const fieldErrors = errors[field];
      if (Array.isArray(fieldErrors)) {
        messages.push(...fieldErrors);
      }
    }
    if (messages.length > 0) return messages.join(". ");
  }

  // RFC 7807 Problem Details
  if (typeof data.detail === "string" && data.detail) return data.detail;
  if (typeof data.title === "string" && data.title) return data.title;

  // Generic message
  if (typeof data.message === "string" && data.message) return data.message;

  return `Request failed (${status})`;
}

// Auth endpoints that return 401 for bad credentials (NOT expired tokens)
const AUTH_PATHS = ["/auth/login", "/auth/register", "/auth/google"];

async function baseFetch<T>(
  path: string,
  options: FetchOptions = {}
): Promise<ApiResponse<T>> {
  const {
    method = "GET",
    body,
    headers = {},
    timeout = TIMEOUT_MS,
    skipAuth = false,
  } = options;

  const url = path.startsWith("http") ? path : `${API_BASE}${path}`;

  // Check if this is an auth endpoint (should not auto-refresh on 401)
  const isAuthEndpoint = AUTH_PATHS.some((p) => path.endsWith(p));

  // Build headers
  const reqHeaders: Record<string, string> = { ...headers };

  if (body && !(body instanceof FormData)) {
    reqHeaders["Content-Type"] = "application/json";
  }

  if (!skipAuth) {
    const token = getAccessToken();
    if (token) {
      reqHeaders["Authorization"] = `Bearer ${token}`;
    }
  }

  // Build request
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), timeout);

  try {
    const res = await fetch(url, {
      method,
      headers: reqHeaders,
      body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined,
      signal: controller.signal,
    });

    clearTimeout(timeoutId);

    // Handle 401 — but NOT for auth endpoints (they return 401 for bad creds)
    if (res.status === 401 && !skipAuth && !isAuthEndpoint && isClient) {
      const newToken = await getRefreshedToken();
      if (newToken) {
        // Retry with new token
        reqHeaders["Authorization"] = `Bearer ${newToken}`;
        const retryRes = await fetch(url, {
          method,
          headers: reqHeaders,
          body: body instanceof FormData ? body : body ? JSON.stringify(body) : undefined,
        });

        if (!retryRes.ok) {
          const errData = await retryRes.json().catch(() => ({}));
          throw new ApiError(retryRes.status, parseErrorBody(errData, retryRes.status));
        }

        return retryRes.json();
      } else {
        // Refresh failed
        clearTokens();
        if (isClient && window.location.pathname.startsWith("/favorites")) {
          const webUrl = process.env.NEXT_PUBLIC_WEB_URL || "http://localhost:3000";
          window.location.href = `${webUrl}/login?redirect=collection-relay`;
        }
        throw new ApiError(401, "Session expired. Please login again.");
      }
    }

    if (!res.ok) {
      const errData = await res.json().catch(() => ({}));
      throw new ApiError(res.status, parseErrorBody(errData, res.status));
    }

    // Handle 204 No Content
    if (res.status === 204) {
      return { success: true } as ApiResponse<T>;
    }

    return res.json();
  } catch (err) {
    clearTimeout(timeoutId);

    if (err instanceof ApiError) throw err;

    if (err instanceof DOMException && err.name === "AbortError") {
      throw new ApiError(408, "Request timed out");
    }

    throw new ApiError(0, "Network error. Please check your connection.");
  }
}

// ─── Public API ────────────────────────────────────────────

export const api = {
  get<T>(path: string, params?: Record<string, string | number | boolean | undefined>) {
    let url = path;
    if (params) {
      const searchParams = new URLSearchParams();
      Object.entries(params).forEach(([key, value]) => {
        if (value !== undefined && value !== "") {
          searchParams.set(key, String(value));
        }
      });
      const qs = searchParams.toString();
      if (qs) url += `?${qs}`;
    }
    return baseFetch<T>(url);
  },

  post<T>(path: string, body?: unknown) {
    return baseFetch<T>(path, { method: "POST", body });
  },

  put<T>(path: string, body?: unknown) {
    return baseFetch<T>(path, { method: "PUT", body });
  },

  delete<T>(path: string) {
    return baseFetch<T>(path, { method: "DELETE" });
  },

  /**
   * Upload a file via multipart/form-data.
   * Pass a FormData object directly.
   */
  upload<T>(path: string, formData: FormData) {
    return baseFetch<T>(path, {
      method: "POST",
      body: formData,
    });
  },

  /**
   * Upload with PUT method (for updates)
   */
  uploadPut<T>(path: string, formData: FormData) {
    return baseFetch<T>(path, {
      method: "PUT",
      body: formData,
    });
  },
};

export { ApiError };
