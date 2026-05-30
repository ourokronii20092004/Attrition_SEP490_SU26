import { ChevronLeft, ChevronRight } from "lucide-react";

interface PaginationProps {
  page: number;
  totalPages: number;
  onChange: (page: number) => void;
}

export function Pagination({ page, totalPages, onChange }: PaginationProps) {
  if (totalPages <= 1) return null;
  return (
    <nav className="mt-10 flex items-center justify-center gap-2" aria-label="Pagination">
      <button
        onClick={() => onChange(Math.max(1, page - 1))}
        disabled={page <= 1}
        className="inline-flex h-9 items-center gap-1 rounded-md border border-border px-3 text-sm text-fg-muted transition-colors hover:border-accent hover:text-accent disabled:pointer-events-none disabled:opacity-40"
      >
        <ChevronLeft size={16} /> Prev
      </button>
      <span className="px-3 text-sm tabular-nums text-fg-muted">
        {page} <span className="text-fg-subtle">/</span> {totalPages}
      </span>
      <button
        onClick={() => onChange(Math.min(totalPages, page + 1))}
        disabled={page >= totalPages}
        className="inline-flex h-9 items-center gap-1 rounded-md border border-border px-3 text-sm text-fg-muted transition-colors hover:border-accent hover:text-accent disabled:pointer-events-none disabled:opacity-40"
      >
        Next <ChevronRight size={16} />
      </button>
    </nav>
  );
}
