import { describe, it, expect, vi, afterEach } from "vitest";
import { liveWhenFocused, LIVE_FAST, LIVE_NORMAL, LIVE_SLOW } from "./live";

// Tests run in the node environment without jsdom (same as client.test.ts), so `document` is
// stubbed per-test rather than provided by a DOM. That also lets the no-document case — the
// server-render path — be exercised directly.
function withFocus(hasFocus: boolean) {
  vi.stubGlobal("document", { hasFocus: () => hasFocus });
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe("liveWhenFocused", () => {
  it("polls at the given interval while the tab is focused", () => {
    withFocus(true);
    expect(liveWhenFocused(LIVE_FAST)()).toBe(LIVE_FAST);
  });

  it("stops polling when the tab loses focus", () => {
    withFocus(false);
    // false (not 0) is what TanStack Query treats as "disabled"; 0 would poll continuously.
    expect(liveWhenFocused(LIVE_FAST)()).toBe(false);
  });

  it("does not poll when there is no document, so SSR never schedules a refetch", () => {
    vi.stubGlobal("document", undefined);
    expect(liveWhenFocused(LIVE_FAST)()).toBe(false);
  });

  it("returns a fresh decision on every call, not a captured one", () => {
    const poll = liveWhenFocused(LIVE_NORMAL);
    withFocus(true);
    expect(poll()).toBe(LIVE_NORMAL);
    withFocus(false);
    expect(poll()).toBe(false);
    withFocus(true);
    expect(poll()).toBe(LIVE_NORMAL);
  });

  it("preserves whichever cadence it was given", () => {
    withFocus(true);
    expect(liveWhenFocused(LIVE_SLOW)()).toBe(LIVE_SLOW);
  });
});

describe("live cadences", () => {
  it("orders fast through slow, so a mixed-up import is obvious", () => {
    expect(LIVE_FAST).toBeLessThan(LIVE_NORMAL);
    expect(LIVE_NORMAL).toBeLessThan(LIVE_SLOW);
  });

  it("keeps the fast cadence above a threshold that would hammer the API", () => {
    expect(LIVE_FAST).toBeGreaterThanOrEqual(3_000);
  });
});
