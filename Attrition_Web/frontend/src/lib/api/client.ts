import { API_BASE } from "../config";

let accessToken: string | null = null;
let refreshToken: string | null = null;
let refreshPromise: Promise<boolean> | null = null;

const TOKEN_KEY = "attrition_access_token";
const REFRESH_KEY = "attrition_refresh_token";

export function loadTokens() {
  if (typeof window === "undefined") return;
  accessToken = localStorage.getItem(TOKEN_KEY);
  refreshToken = localStorage.getItem(REFRESH_KEY);
}

export function setTokens(access: string, refresh: string) {
  accessToken = access;
  refreshToken = refresh;
  if (typeof window !== "undefined") {
    localStorage.setItem(TOKEN_KEY, access);
    localStorage.setItem(REFRESH_KEY, refresh);
  }
}

export function clearTokens() {
  accessToken = null;
  refreshToken = null;
  if (typeof window !== "undefined") {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
  }
}

export function getAccessToken() {
  return accessToken;
}

async function attemptRefresh(): Promise<boolean> {
  if (!refreshToken) return false;
  try {
    const res = await fetch(`${API_BASE}/api/auth/refresh`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    });
    if (!res.ok) return false;
    const json = await res.json();
    if (json.success && json.data) {
      setTokens(json.data.accessToken, json.data.refreshToken);
      return true;
    }
    return false;
  } catch {
    return false;
  }
}

async function refreshIfNeeded(): Promise<boolean> {
  if (!refreshPromise) {
    refreshPromise = attemptRefresh().finally(() => {
      refreshPromise = null;
    });
  }
  return refreshPromise;
}

export class ApiError extends Error {
  constructor(
    public status: number,
    public body: string,
  ) {
    super(`API ${status}: ${body}`);
    this.name = "ApiError";
  }
}

interface FetchOptions extends Omit<RequestInit, "body"> {
  body?: unknown;
  auth?: boolean;
}

export async function apiFetch<T>(
  path: string,
  opts: FetchOptions = {},
): Promise<T> {
  const { body, auth = true, headers: extraHeaders, ...rest } = opts;

  const url = `${API_BASE}${path}`;
  const headers: Record<string, string> = { ...(extraHeaders as Record<string, string>) };

  if (body !== undefined && !(body instanceof FormData)) {
    headers["Content-Type"] = "application/json";
  }

  if (auth && accessToken) {
    headers["Authorization"] = `Bearer ${accessToken}`;
  }

  const init: RequestInit = {
    ...rest,
    headers,
    body: body instanceof FormData ? body : body !== undefined ? JSON.stringify(body) : undefined,
  };

  let res = await fetch(url, init);

  if (res.status === 401 && auth && refreshToken) {
    const refreshed = await refreshIfNeeded();
    if (refreshed) {
      headers["Authorization"] = `Bearer ${accessToken}`;
      res = await fetch(url, { ...init, headers });
    } else {
      // Refresh failed (expired/invalid). Clear stale tokens and signal a session reset
      // so AuthProvider can drop the user instead of looping failed refreshes forever.
      clearTokens();
      if (typeof window !== "undefined") {
        window.dispatchEvent(new Event("attrition:session-expired"));
      }
    }
  }

  if (!res.ok) {
    const text = await res.text().catch(() => "");
    throw new ApiError(res.status, text);
  }

  const contentType = res.headers.get("content-type") ?? "";
  if (contentType.includes("application/json")) {
    return res.json();
  }
  return res.text() as unknown as T;
}
