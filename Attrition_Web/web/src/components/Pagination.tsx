import React from "react";
import Link from "next/link";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { cn } from "@/lib/utils";

interface PaginationProps {
  currentPage: number;
  totalCount: number;
  pageSize: number;
  baseUrl: string;
  searchParams?: URLSearchParams | Record<string, string>;
}

export function Pagination({ currentPage, totalCount, pageSize, baseUrl, searchParams }: PaginationProps) {
  const totalPages = Math.ceil(totalCount / pageSize);
  if (totalPages <= 1) return null;

  const buildUrl = (page: number) => {
    const params = new URLSearchParams(searchParams as Record<string, string>);
    params.set("page", page.toString());
    return `${baseUrl}?${params.toString()}`;
  };

  const pages = [];
  // simple logic to show max 5 pages around current
  let start = Math.max(1, currentPage - 2);
  let end = Math.min(totalPages, start + 4);
  if (end - start < 4) start = Math.max(1, end - 4);

  for (let i = start; i <= end; i++) {
    pages.push(i);
  }

  return (
    <div style={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 8, marginTop: "var(--space-6)" }}>
      {currentPage > 1 ? (
        <Link href={buildUrl(currentPage - 1)} className="btn btn-secondary btn-sm" style={{ padding: "0 var(--space-2)" }}>
          <ChevronLeft size={16} />
        </Link>
      ) : (
        <button className="btn btn-secondary btn-sm" disabled style={{ padding: "0 var(--space-2)" }}>
          <ChevronLeft size={16} />
        </button>
      )}

      {start > 1 && (
        <>
          <Link href={buildUrl(1)} className="btn btn-secondary btn-sm">1</Link>
          {start > 2 && <span style={{ color: "var(--text-muted)", padding: "0 4px" }}>...</span>}
        </>
      )}

      {pages.map((p) => (
        <Link
          key={p}
          href={buildUrl(p)}
          className={cn("btn btn-sm", p === currentPage ? "btn-primary" : "btn-secondary")}
        >
          {p}
        </Link>
      ))}

      {end < totalPages && (
        <>
          {end < totalPages - 1 && <span style={{ color: "var(--text-muted)", padding: "0 4px" }}>...</span>}
          <Link href={buildUrl(totalPages)} className="btn btn-secondary btn-sm">{totalPages}</Link>
        </>
      )}

      {currentPage < totalPages ? (
        <Link href={buildUrl(currentPage + 1)} className="btn btn-secondary btn-sm" style={{ padding: "0 var(--space-2)" }}>
          <ChevronRight size={16} />
        </Link>
      ) : (
        <button className="btn btn-secondary btn-sm" disabled style={{ padding: "0 var(--space-2)" }}>
          <ChevronRight size={16} />
        </button>
      )}
    </div>
  );
}
