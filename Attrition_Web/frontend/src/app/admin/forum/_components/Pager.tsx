"use client";

import { Pagination } from "@/components/ui/pagination";

/** Shared admin pager — page numbers + prev/next. Wraps the global Pagination component. */
export function Pager({ page, totalPages, onPage }: { page: number; totalPages: number; onPage: (p: number) => void }) {
  return <Pagination page={page} totalPages={totalPages} onChange={onPage} compact />;
}
