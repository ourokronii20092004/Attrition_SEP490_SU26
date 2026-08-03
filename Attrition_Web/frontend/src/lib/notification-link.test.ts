import { describe, it, expect } from "vitest";
import { threadIdFromNotificationLink } from "./notification-link";

const ID = "3f2504e0-4f89-11d3-9a0c-0305e82c3301";

describe("threadIdFromNotificationLink", () => {
  it("pulls the thread id out of a reply deep link", () => {
    expect(threadIdFromNotificationLink(`/forum/${ID}#post-abc`)).toBe(ID);
  });

  it("handles a bare thread link with no post anchor", () => {
    expect(threadIdFromNotificationLink(`/forum/${ID}`)).toBe(ID);
  });

  it("handles a trailing slash and a query string", () => {
    expect(threadIdFromNotificationLink(`/forum/${ID}/`)).toBe(ID);
    expect(threadIdFromNotificationLink(`/forum/${ID}?page=2`)).toBe(ID);
  });

  it("tolerates surrounding whitespace", () => {
    expect(threadIdFromNotificationLink(`  /forum/${ID}#post-1  `)).toBe(ID);
  });

  it("returns null for links that aren't forum threads", () => {
    expect(threadIdFromNotificationLink("/u/someone")).toBeNull();
    expect(threadIdFromNotificationLink("/wiki/archdemon")).toBeNull();
    expect(threadIdFromNotificationLink("/forum")).toBeNull();
    expect(threadIdFromNotificationLink("/forum/")).toBeNull();
  });

  it("returns null when the segment isn't a guid", () => {
    expect(threadIdFromNotificationLink("/forum/not-a-guid")).toBeNull();
    expect(threadIdFromNotificationLink("/forum/12345")).toBeNull();
  });

  it("does not match a thread id embedded deeper in the path", () => {
    // Guards against a future /forum/categories/{id} style route being mistaken for a thread.
    expect(threadIdFromNotificationLink(`/forum/categories/${ID}`)).toBeNull();
  });

  it("returns null for missing or empty input", () => {
    expect(threadIdFromNotificationLink(null)).toBeNull();
    expect(threadIdFromNotificationLink(undefined)).toBeNull();
    expect(threadIdFromNotificationLink("")).toBeNull();
  });
});
