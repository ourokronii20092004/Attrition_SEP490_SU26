/**
 * Hydration-safe date formatting.
 *
 * React 19 throws a hydration mismatch when the server and client render
 * different text for the same node. `new Date(x).toLocaleString()` does exactly
 * that: the server (Node, often en-US/UTC) and the browser (user locale/timezone)
 * disagree. Pinning BOTH locale and timeZone makes the output deterministic, so
 * the server HTML and the first client render are byte-identical.
 *
 * Use these for any date rendered during render. For "x minutes ago" relative
 * time (which legitimately differs by the clock), use <RelativeTime>, which
 * computes after mount.
 */

const LOCALE = "en-US";
const TZ = "UTC";

export function formatDate(iso: string | number | Date): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleDateString(LOCALE, {
    year: "numeric",
    month: "short",
    day: "numeric",
    timeZone: TZ,
  });
}

export function formatDateTime(iso: string | number | Date): string {
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  return d.toLocaleDateString(LOCALE, {
    year: "numeric",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
    timeZone: TZ,
  });
}
