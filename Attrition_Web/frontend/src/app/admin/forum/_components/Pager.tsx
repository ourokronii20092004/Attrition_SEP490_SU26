"use client";

import { Button } from "@/components/ui/button";

/** Minimal prev/next pager shared by the admin moderation lists. */
export function Pager({ page, totalPages, onPage }: { page: number; totalPages: number; onPage: (p: number) => void }) {
  if (totalPages <= 1) return null;
  return (
    <div className="flex items-center justify-center gap-3 pt-2">
      <Button size="sm" variant="secondary" disabled={page <= 1} onClick={() => onPage(page - 1)}>Previous</Button>
      <span className="text-sm text-fg-muted">Page {page} of {totalPages}</span>
      <Button size="sm" variant="secondary" disabled={page >= totalPages} onClick={() => onPage(page + 1)}>Next</Button>
    </div>
  );
}
