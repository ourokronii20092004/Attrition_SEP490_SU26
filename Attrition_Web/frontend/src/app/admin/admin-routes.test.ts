import { describe, expect, it, beforeEach } from "vitest";
import { adminTrailFor, setAdminPageLabel } from "./admin-routes";

// The suite runs in vitest's `node` environment (see vitest.config.ts), so the two browser globals
// this module touches are stubbed here rather than pulling jsdom in for one file. Only the members
// admin-routes actually uses are provided.
const store = new Map<string, string>();
Object.assign(globalThis, {
  window: { dispatchEvent: () => true, localStorage: undefined as unknown },
  localStorage: {
    getItem: (k: string) => store.get(k) ?? null,
    setItem: (k: string, v: string) => void store.set(k, v),
    removeItem: (k: string) => void store.delete(k),
    clear: () => store.clear(),
  },
  CustomEvent: class {
    constructor(public type: string) {}
  },
});

/** The trail as "A › B › C", which is what a reader actually checks. */
const render = (path: string) => adminTrailFor(path).map((c) => c.label).join(" › ");

describe("adminTrailFor", () => {
  beforeEach(() => store.clear());

  it("is just Dashboard at the admin root", () => {
    expect(render("/admin")).toBe("Dashboard");
    expect(adminTrailFor("/admin")).toEqual([{ href: "/admin", label: "Dashboard" }]);
  });

  it("shows a top-level page under Dashboard", () => {
    expect(render("/admin/users")).toBe("Dashboard › Users");
  });

  it("keeps the parent section for a detail page", () => {
    // The bug this guards: a room detail used to read "Dashboard › <code>", losing Co-op Rooms.
    // Before the name resolves, the leaf is a plain "Detail" — the parent crumb carries the section,
    // so it is not repeated here.
    expect(render("/admin/rooms/8d1f4a2b-1111-4222-8333-444455556666"))
      .toBe("Dashboard › Co-op Rooms › Detail");
  });

  it("uses a registered entity name for the last crumb", () => {
    const path = "/admin/rooms/8d1f4a2b-1111-4222-8333-444455556666";
    setAdminPageLabel(path, "ABC123");
    expect(render(path)).toBe("Dashboard › Co-op Rooms › ABC123");
  });

  it("does not repeat a section name the parent crumb already shows", () => {
    const path = "/admin/forum/threads/7c2e0000-2222-4333-8444-555566667777";
    setAdminPageLabel(path, "Threads · Why is the boss so hard");
    expect(render(path)).toBe("Dashboard › Forum › Threads › Why is the boss so hard");
  });

  it("links every crumb but the last to its own page", () => {
    const trail = adminTrailFor("/admin/music/albums");
    expect(trail.map((c) => c.href)).toEqual(["/admin", "/admin/music", "/admin/music/albums"]);
  });

  it("skips an intermediate segment that isn't a real page", () => {
    // /admin/users/<id>/sessions — "sessions" has no route, but the leaf must still appear.
    const trail = adminTrailFor("/admin/users/1111aaaa-3333-4444-8555-666677778888");
    expect(trail).toHaveLength(3);
    expect(trail[1].href).toBe("/admin/users");
  });
});
