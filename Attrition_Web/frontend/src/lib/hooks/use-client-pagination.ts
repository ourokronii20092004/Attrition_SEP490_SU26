"use client";

import { useEffect, useMemo, useState } from "react";

/**
 * Client-side pagination for admin list pages that fetch their full dataset at once (enemies,
 * items, tracks, wiki articles, etc.). Slices an already-filtered array into pages and resets to
 * page 1 whenever the filtered length changes (e.g. a search narrows the list).
 */
export function useClientPagination<T>(items: T[], pageSize = 20) {
  const [page, setPage] = useState(1);
  const totalPages = Math.max(1, Math.ceil(items.length / pageSize));

  // Snap back into range when the dataset shrinks (filtering/deletion).
  useEffect(() => {
    if (page > totalPages) setPage(totalPages);
  }, [page, totalPages]);

  const paged = useMemo(
    () => items.slice((page - 1) * pageSize, page * pageSize),
    [items, page, pageSize]
  );

  return { page, setPage, totalPages, paged };
}
