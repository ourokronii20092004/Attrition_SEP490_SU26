import { API_BASE } from "../config";

// Auth is carried by HttpOnly cookies set by the API (access + refresh). We never read or
// store tokens in JS anymore. The only JS-readable cookie is the CSRF token, which we echo
// back in the X-CSRF header on mutating requests (double-submit pattern).
let refreshPromise: Promise<boolean> | null = null;

const CSRF_COOKIE = "attrition_csrf";

function readCookie(name: string): string | null {
  if (typeof document === "undefined") return null;
  const match = document.cookie.match(new RegExp(`(?:^|; )${name}=([^;]*)`));
  return match ? decodeURIComponent(match[1]) : null;
}

/** Fetches a fresh CSRF cookie if we don't already have one (e.g. first mutating call). */
async function ensureCsrf(): Promise<void> {
  if (readCookie(CSRF_COOKIE)) return;
  try {
    await fetch(`${API_BASE}/api/auth/csrf`, { credentials: "include" });
  } catch {
    // Non-fatal: the request may still succeed if the server doesn't require CSRF here.
  }
}

async function attemptRefresh(): Promise<boolean> {
  try {
    const res = await fetch(`${API_BASE}/api/auth/refresh`, {
      method: "POST",
      credentials: "include",
      headers: { "Content-Type": "application/json" },
      body: "{}",
    });
    return res.ok;
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

const SAFE_METHODS = new Set(["GET", "HEAD", "OPTIONS"]);

export async function apiFetch<T>(
  path: string,
  opts: FetchOptions = {},
): Promise<T> {
  const { body, auth = true, headers: extraHeaders, method = "GET", ...rest } = opts;

  const url = `${API_BASE}${path}`;
  const headers: Record<string, string> = { ...(extraHeaders as Record<string, string>) };

  if (body !== undefined && !(body instanceof FormData)) {
    headers["Content-Type"] = "application/json";
  }

  // Double-submit CSRF: echo the readable CSRF cookie on mutating requests.
  if (!SAFE_METHODS.has(method.toUpperCase())) {
    await ensureCsrf();
    const csrf = readCookie(CSRF_COOKIE);
    if (csrf) headers["X-CSRF"] = csrf;
  }

  const init: RequestInit = {
    ...rest,
    method,
    credentials: "include",
    headers,
    body: body instanceof FormData ? body : body !== undefined ? JSON.stringify(body) : undefined,
  };

  let res = await fetch(url, init);

  if (res.status === 401 && auth) {
    const refreshed = await refreshIfNeeded();
    if (refreshed) {
      res = await fetch(url, init);
    } else {
      // Refresh failed (expired/invalid). Signal a session reset so AuthProvider drops the user.
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
