"use client";

import { useEffect, useState } from "react";
import { formatDateTime } from "@/lib/format-date";

/**
 * Renders a timestamp as "2m ago" — but only AFTER mount.
 *
 * On the server and first client paint it renders the deterministic absolute
 * string (fixed locale + UTC), so hydration matches exactly. A useEffect then
 * swaps in the relative phrasing, which depends on the live clock and would
 * otherwise cause a hydration mismatch.
 */
export function RelativeTime({ iso, className }: { iso: string; className?: string }) {
  const [relative, setRelative] = useState<string | null>(null);

  useEffect(() => {
    setRelative(toRelative(new Date(iso).getTime()));
  }, [iso]);

  const absolute = formatDateTime(iso);

  return (
    <time dateTime={iso} title={absolute} className={className} suppressHydrationWarning>
      {relative ?? absolute}
    </time>
  );
}

function toRelative(ms: number): string {
  const diff = Date.now() - ms;
  if (Number.isNaN(diff)) return "";
  const sec = Math.round(diff / 1000);
  if (sec < 60) return "just now";
  const min = Math.round(sec / 60);
  if (min < 60) return `${min}m ago`;
  const hr = Math.round(min / 60);
  if (hr < 24) return `${hr}h ago`;
  const day = Math.round(hr / 24);
  if (day < 30) return `${day}d ago`;
  const mo = Math.round(day / 30);
  if (mo < 12) return `${mo}mo ago`;
  return `${Math.round(mo / 12)}y ago`;
}
