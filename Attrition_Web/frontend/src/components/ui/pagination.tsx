import { ChevronLeft, ChevronRight } from "lucide-react";

interface PaginationProps {
  page: number;
  totalPages: number;
  onChange: (page: number) => void;
  /** When true, lays out tighter (no top margin) for use inside dense admin tables. */
  compact?: boolean;
}

/** Build a windowed list of page numbers with "…" gaps, always showing first/last. */
function pageWindow(page: number, total: number): (number | "…")[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);
  const out: (number | "…")[] = [1];
  const start = Math.max(2, page - 1);
  const end = Math.min(total - 1, page + 1);
  if (start > 2) out.push("…");
  for (let i = start; i <= end; i++) out.push(i);
  if (end < total - 1) out.push("…");
  out.push(total);
  return out;
}

export function Pagination({ page, totalPages, onChange, compact = false }: PaginationProps) {
  if (totalPages <= 1) return null;
  const pages = pageWindow(page, totalPages);
  const arrowCls =
    "inline-flex h-9 items-center gap-1 rounded-md border border-border px-2.5 text-sm text-fg-muted transition-colors hover:border-accent hover:text-accent disabled:pointer-events-none disabled:opacity-40";

  return (
    <nav className={`flex flex-wrap items-center justify-center gap-1.5 ${compact ? "mt-4" : "mt-10"}`} aria-label="Pagination">
      <button onClick={() => onChange(Math.max(1, page - 1))} disabled={page <= 1} className={arrowCls} aria-label="Previous page">
        <ChevronLeft size={16} /> <span className="hidden sm:inline">Prev</span>
      </button>

      {pages.map((p, i) =>
        p === "…" ? (
          <span key={`gap-${i}`} className="px-1.5 text-sm text-fg-subtle">…</span>
        ) : (
          <button
            key={p}
            onClick={() => onChange(p)}
            aria-current={p === page ? "page" : undefined}
            className={`h-9 min-w-9 rounded-md border px-2 text-sm tabular-nums transition-colors ${
              p === page
                ? "border-accent bg-accent text-accent-fg"
                : "border-border text-fg-muted hover:border-accent hover:text-accent"
            }`}
          >
            {p}
          </button>
        )
      )}

      <button onClick={() => onChange(Math.min(totalPages, page + 1))} disabled={page >= totalPages} className={arrowCls} aria-label="Next page">
        <span className="hidden sm:inline">Next</span> <ChevronRight size={16} />
      </button>
    </nav>
  );
}
