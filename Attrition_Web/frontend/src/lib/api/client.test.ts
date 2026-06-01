import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

// The client reads document.cookie and dispatches a window event; give it a minimal DOM-less
// shim so we can run in the node environment without jsdom.
let cookieJar = "";
beforeEach(() => {
  vi.resetModules(); // fresh module state (refreshPromise) per test
  cookieJar = "";
  vi.stubGlobal("document", {
    get cookie() { return cookieJar; },
    set cookie(v: string) { cookieJar = v; },
  });
  const listeners: Record<string, Array<() => void>> = {};
  vi.stubGlobal("window", {
    addEventListener: (t: string, cb: () => void) => { (listeners[t] ??= []).push(cb); },
    removeEventListener: () => {},
    dispatchEvent: (e: { type: string }) => { (listeners[e.type] ?? []).forEach((c) => c()); return true; },
  });
  vi.stubGlobal("Event", class { type: string; constructor(t: string) { this.type = t; } } as unknown as typeof Event);
});
afterEach(() => { vi.unstubAllGlobals(); vi.restoreAllMocks(); });

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });
}

describe("apiFetch", () => {
  it("does not send X-CSRF on a GET", async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ ok: 1 }));
    vi.stubGlobal("fetch", fetchMock);
    const { apiFetch } = await import("./client");

    await apiFetch("/api/thing");
    const init = fetchMock.mock.calls[0][1];
    expect(init.method).toBe("GET");
    expect(init.headers["X-CSRF"]).toBeUndefined();
    expect(init.credentials).toBe("include");
  });

  it("sends the decoded X-CSRF header from the cookie on a POST", async () => {
    cookieJar = "attrition_csrf=ab%2Bcd%3D"; // url-encoded "ab+cd="
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({ ok: 1 }));
    vi.stubGlobal("fetch", fetchMock);
    const { apiFetch } = await import("./client");

    await apiFetch("/api/thing", { method: "POST", body: { a: 1 } });
    const init = fetchMock.mock.calls[0][1];
    expect(init.headers["X-CSRF"]).toBe("ab+cd="); // decoded, matches what the server stored
  });

  it("on 401 it refreshes once then retries the original request", async () => {
    cookieJar = "attrition_csrf=tok";
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response("", { status: 401 }))        // original
      .mockResolvedValueOnce(jsonResponse({ success: true }))           // /refresh
      .mockResolvedValueOnce(jsonResponse({ ok: "retried" }));          // retry
    vi.stubGlobal("fetch", fetchMock);
    const { apiFetch } = await import("./client");

    const result = await apiFetch<{ ok: string }>("/api/secure");
    expect(result.ok).toBe("retried");
    const urls = fetchMock.mock.calls.map((c) => String(c[0]));
    expect(urls.some((u) => u.includes("/api/auth/refresh"))).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("on 401 with a failed refresh it dispatches session-expired and throws", async () => {
    const expired = vi.fn();
    (window.addEventListener as (t: string, cb: () => void) => void)("attrition:session-expired", expired);
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response("", { status: 401 }))             // original
      .mockResolvedValueOnce(new Response("", { status: 401 }));            // /refresh fails
    vi.stubGlobal("fetch", fetchMock);
    const { apiFetch, ApiError } = await import("./client");

    await expect(apiFetch("/api/secure")).rejects.toBeInstanceOf(ApiError);
    expect(expired).toHaveBeenCalled();
  });
});
