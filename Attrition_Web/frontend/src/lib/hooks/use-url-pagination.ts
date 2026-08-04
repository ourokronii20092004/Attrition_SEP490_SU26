"use client";

import { useCallback, useEffect, useMemo } from "react";
import { usePathname, useRouter, useSearchParams } from "next/navigation";

/**
 * List state (page number, search, filters) kept in the URL query string.
 *
 * `useState` loses everything the moment the component unmounts, so opening a detail page and
 * coming back dropped the reader on page 1 with their filters cleared — on a long list that means
 * hunting for their place again. The URL survives navigation, Back, refresh, and being shared,
 * which is what people already expect a list page to do.
 *
 * Values equal to their default are omitted from the query, so an untouched list stays on a clean
 * URL rather than accumulating `?page=1&search=`.
 */

/** Read a string param, falling back when absent or empty. */
export function useQueryParam(key: string, fallback = "") {
  const searchParams = useSearchParams();
  const setParam = useSetQueryParams();
  const value = searchParams.get(key) ?? fallback;

  const set = useCallback(
    (next: string) => {
      // Any filter change invalidates the current page: page 7 of the old result set is
      // meaningless against the new one, and often past its end.
      setParam({ [key]: next || null, page: null });
    },
    [key, setParam],
  );

  return [value, set] as const;
}

/**
 * Patch query params without disturbing the rest. A null value removes the key.
 *
 * Uses `replace` rather than `push`: typing in a search box would otherwise stack one history
 * entry per keystroke, and Back would walk through them one character at a time.
 */
export function useSetQueryParams() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  return useCallback(
    (patch: Record<string, string | number | null>) => {
      const params = new URLSearchParams(searchParams.toString());
      for (const [key, value] of Object.entries(patch)) {
        if (value === null || value === "") params.delete(key);
        else params.set(key, String(value));
      }
      const qs = params.toString();
      router.replace(qs ? `${pathname}?${qs}` : pathname, { scroll: false });
    },
    [router, pathname, searchParams],
  );
}

/**
 * Client-side pagination whose page number lives in the URL.
 *
 * Drop-in replacement for the previous `useState` version: same `{ page, setPage, totalPages,
 * paged }` shape, so call sites only change their import.
 */
export function useUrlPagination<T>(items: T[], pageSize = 20) {
  const searchParams = useSearchParams();
  const setParams = useSetQueryParams();

  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));

  const raw = Number(searchParams.get("page"));
  // Clamp rather than trust: ?page=0, ?page=abc and ?page=999 are all reachable by hand.
  const page = Number.isFinite(raw) && raw >= 1 ? Math.min(Math.floor(raw), totalPages) : 1;

  const setPage = useCallback(
    (next: number) => {
      const clamped = Math.min(Math.max(1, Math.floor(next)), totalPages);
      // Page 1 is the default, so it stays out of the URL.
      setParams({ page: clamped === 1 ? null : clamped });
    },
    [setParams, totalPages],
  );

  // If the data shrinks under us (a filter narrows it, a row is deleted) and the URL still points
  // past the end, rewrite it to the last real page instead of rendering an empty list.
  useEffect(() => {
    if (raw > totalPages && totalPages >= 1) {
      setParams({ page: totalPages === 1 ? null : totalPages });
    }
  }, [raw, totalPages, setParams]);

  const paged = useMemo(
    () => items.slice((page - 1) * pageSize, page * pageSize),
    [items, page, pageSize],
  );

  return { page, setPage, totalPages, paged };
}

/**
 * Just the page number from the URL, for lists the server pages (the query takes `page`, so the
 * data isn't sliced client-side and `totalPages` comes from the response).
 *
 * `totalPages` is optional because it usually isn't known until the first response lands; pass it
 * once available and out-of-range values get corrected the same way as `useUrlPagination`.
 */
export function useUrlPage(totalPages?: number) {
  const searchParams = useSearchParams();
  const setParams = useSetQueryParams();

  const raw = Number(searchParams.get("page"));
  const requested = Number.isFinite(raw) && raw >= 1 ? Math.floor(raw) : 1;
  const page = totalPages && totalPages >= 1 ? Math.min(requested, totalPages) : requested;

  const setPage = useCallback(
    (next: number) => {
      const floored = Math.max(1, Math.floor(next));
      const clamped = totalPages && totalPages >= 1 ? Math.min(floored, totalPages) : floored;
      setParams({ page: clamped === 1 ? null : clamped });
    },
    [setParams, totalPages],
  );

  // A hand-typed or now-stale ?page beyond the end gets rewritten once the real total is known.
  useEffect(() => {
    if (totalPages && totalPages >= 1 && requested > totalPages) {
      setParams({ page: totalPages === 1 ? null : totalPages });
    }
  }, [requested, totalPages, setParams]);

  return [page, setPage] as const;
}
