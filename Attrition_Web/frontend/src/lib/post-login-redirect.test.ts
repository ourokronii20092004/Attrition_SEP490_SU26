import { describe, it, expect, vi, afterEach } from "vitest";
import {
  isSafeRedirect,
  loginHref,
  redirectParam,
  safeRedirectTarget,
  stashPendingRedirect,
  takePendingRedirect,
} from "./post-login-redirect";

// Tests run in the node environment without jsdom (same as client.test.ts), so window is stubbed.
function withStorage() {
  const store = new Map<string, string>();
  vi.stubGlobal("window", {
    sessionStorage: {
      getItem: (k: string) => store.get(k) ?? null,
      setItem: (k: string, v: string) => void store.set(k, v),
      removeItem: (k: string) => void store.delete(k),
    },
  });
  return store;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("isSafeRedirect", () => {
  it("accepts same-origin paths, including query and hash", () => {
    expect(isSafeRedirect("/forum/123")).toBe(true);
    expect(isSafeRedirect("/forum/123#post-abc")).toBe(true);
    expect(isSafeRedirect("/search?q=hello")).toBe(true);
  });

  it("rejects absolute URLs to other origins", () => {
    expect(isSafeRedirect("https://evil.com")).toBe(false);
    expect(isSafeRedirect("http://evil.com/path")).toBe(false);
  });

  it("rejects protocol-relative URLs, which browsers treat as off-origin", () => {
    expect(isSafeRedirect("//evil.com")).toBe(false);
    expect(isSafeRedirect("//evil.com/forum/1")).toBe(false);
  });

  it("rejects backslash forms some browsers normalise to //", () => {
    expect(isSafeRedirect("/\\evil.com")).toBe(false);
    expect(isSafeRedirect("/forum\\..\\evil")).toBe(false);
  });

  it("rejects embedded control characters that could smuggle a scheme", () => {
    expect(isSafeRedirect(`/foo${String.fromCharCode(0)}//evil.com`)).toBe(false);
    expect(isSafeRedirect(`/foo${String.fromCharCode(10)}https://evil.com`)).toBe(false);
    expect(isSafeRedirect(`/foo${String.fromCharCode(127)}`)).toBe(false);
  });

  it("rejects the auth routes themselves, so sign-in can't loop", () => {
    expect(isSafeRedirect("/login")).toBe(false);
    expect(isSafeRedirect("/register")).toBe(false);
    expect(isSafeRedirect("/login?redirect=/forum")).toBe(false);
  });

  it("rejects empty and non-path values", () => {
    expect(isSafeRedirect("")).toBe(false);
    expect(isSafeRedirect(null)).toBe(false);
    expect(isSafeRedirect(undefined)).toBe(false);
    expect(isSafeRedirect("forum/123")).toBe(false);
    expect(isSafeRedirect("javascript:alert(1)")).toBe(false);
  });
});

describe("redirectParam / loginHref", () => {
  it("round-trips a deep path through encoding", () => {
    expect(redirectParam("/forum/42")).toBe("?redirect=%2Fforum%2F42");
    expect(loginHref("/forum/42")).toBe("/login?redirect=%2Fforum%2F42");
  });

  it("preserves the query string of the page you were on", () => {
    expect(loginHref("/search", "?q=ren")).toBe("/login?redirect=%2Fsearch%3Fq%3Dren");
  });

  it("omits the param when the current page is an auth page", () => {
    expect(redirectParam("/login")).toBe("");
    expect(loginHref("/login")).toBe("/login");
    expect(loginHref("/register")).toBe("/login");
  });
});

describe("safeRedirectTarget", () => {
  it("returns the target when safe", () => {
    expect(safeRedirectTarget("/forum/7")).toBe("/forum/7");
  });

  it("falls back to the homepage for anything unsafe or missing", () => {
    expect(safeRedirectTarget("https://evil.com")).toBe("/");
    expect(safeRedirectTarget("//evil.com")).toBe("/");
    expect(safeRedirectTarget(null)).toBe("/");
    expect(safeRedirectTarget("/login")).toBe("/");
  });
});

describe("pending redirect (Google round-trip)", () => {
  it("stashes and returns the destination once", () => {
    withStorage();
    stashPendingRedirect("/forum/99");
    expect(takePendingRedirect()).toBe("/forum/99");
    // Consumed: a later visit must not be yanked away to a stale destination.
    expect(takePendingRedirect()).toBeNull();
  });

  it("refuses to stash an unsafe destination", () => {
    withStorage();
    stashPendingRedirect("https://evil.com");
    expect(takePendingRedirect()).toBeNull();
  });

  it("ignores a stashed value that is unsafe when read back", () => {
    const store = withStorage();
    store.set("attrition:post-login-redirect", "//evil.com");
    expect(takePendingRedirect()).toBeNull();
  });

  it("returns null when nothing is pending", () => {
    withStorage();
    expect(takePendingRedirect()).toBeNull();
  });

  it("survives storage being unavailable (private browsing)", () => {
    vi.stubGlobal("window", {
      sessionStorage: {
        getItem: () => { throw new Error("denied"); },
        setItem: () => { throw new Error("denied"); },
        removeItem: () => { throw new Error("denied"); },
      },
    });
    expect(() => stashPendingRedirect("/forum/1")).not.toThrow();
    expect(takePendingRedirect()).toBeNull();
  });

  it("is inert during server rendering, where window does not exist", () => {
    vi.stubGlobal("window", undefined);
    expect(() => stashPendingRedirect("/forum/1")).not.toThrow();
    expect(takePendingRedirect()).toBeNull();
  });
});
