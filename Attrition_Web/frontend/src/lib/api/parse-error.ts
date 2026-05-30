import { ApiError } from "./client";

/**
 * Turn a thrown API error into a human-readable message. Handles both shapes:
 * - our ApiResponse envelope: { error: "..." } or { message: "..." }
 * - ASP.NET validation problem: { errors: { Field: ["msg", ...] } }
 * Falls back to the raw body or a generic message.
 */
export function parseApiError(e: unknown, fallback = "Something went wrong."): string {
  if (!(e instanceof ApiError)) return fallback;
  try {
    const json = JSON.parse(e.body);
    if (json.errors && typeof json.errors === "object") {
      const msgs = Object.values(json.errors as Record<string, string[]>).flat();
      if (msgs.length) return msgs.join(" ");
    }
    return json.error || json.message || json.title || e.body || fallback;
  } catch {
    return e.body || fallback;
  }
}
