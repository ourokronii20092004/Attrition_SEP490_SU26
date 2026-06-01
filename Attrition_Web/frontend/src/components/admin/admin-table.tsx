"use client";

import Link from "next/link";
import { Search, Plus } from "lucide-react";
import { Button } from "@/components/ui/button";

/**
 * Shared admin table-page primitives (the standard format for every admin list page):
 *   1. AdminPageHeader  — page title + a single Add button, on one line (no wasted row).
 *   2. AdminFilterBar   — a search box followed by a row of dropdown filters.
 *   3. AdminTable       — header + clickable rows; row click opens detail, edit/delete sit on the side.
 *
 * These exist so admin pages are uniform and dense, not each hand-rolled differently.
 */

export function AdminPageHeader({ title, addLabel, onAdd, addHref }: {
  title: string;
  addLabel?: string;
  onAdd?: () => void;
  addHref?: string;
}) {
  const addBtn = addLabel ? (
    <Button size="sm" onClick={onAdd}><Plus size={15} className="mr-1.5" />{addLabel}</Button>
  ) : null;
  return (
    <div className="flex items-center justify-between gap-4">
      <h1 className="font-display text-2xl font-bold text-fg">{title}</h1>
      {addHref && addLabel ? <Link href={addHref}>{addBtn}</Link> : addBtn}
    </div>
  );
}

export type FilterDropdown = {
  value: string;
  onChange: (v: string) => void;
  options: { value: string; label: string }[];
  ariaLabel: string;
};

/** Search box + a row of dropdown filters. Second-tier search/filter for any list page. */
export function AdminFilterBar({ search, onSearch, searchPlaceholder = "Search…", filters = [], children }: {
  search?: string;
  onSearch?: (v: string) => void;
  searchPlaceholder?: string;
  filters?: FilterDropdown[];
  children?: React.ReactNode;
}) {
  return (
    <div className="mt-4 flex flex-wrap items-center gap-2">
      {onSearch && (
        <div className="relative min-w-0 flex-1 basis-56">
          <Search size={15} className="pointer-events-none absolute left-2.5 top-1/2 -translate-y-1/2 text-fg-subtle" />
          <input
            value={search ?? ""}
            onChange={(e) => onSearch(e.target.value)}
            placeholder={searchPlaceholder}
            className="w-full rounded-md border border-border bg-surface-2 py-1.5 pl-8 pr-3 text-sm text-fg outline-none transition-colors placeholder:text-fg-subtle focus:border-accent focus:ring-1 focus:ring-accent"
          />
        </div>
      )}
      {filters.map((f, i) => (
        <select
          key={i}
          value={f.value}
          onChange={(e) => f.onChange(e.target.value)}
          aria-label={f.ariaLabel}
          className="rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-fg"
        >
          {f.options.map((o) => <option key={o.value} value={o.value}>{o.label}</option>)}
        </select>
      ))}
      {children}
    </div>
  );
}

/** Table shell: pass column headers and rows. Rows are clickable when onRowClick is given;
 * put edit/delete buttons in the last cell (they should stopPropagation). */
export function AdminTable({ columns, children, empty }: {
  columns: { key: string; label: string; align?: "left" | "right" }[];
  children: React.ReactNode;
  empty?: boolean;
}) {
  return (
    <div className="mt-4 overflow-x-auto rounded-lg border border-border">
      <table className="w-full text-sm">
        <thead>
          <tr className="border-b border-border bg-surface-2/50 text-left text-xs uppercase tracking-wider text-fg-subtle">
            {columns.map((c) => (
              <th key={c.key} className={`px-3 py-2 font-medium ${c.align === "right" ? "text-right" : ""}`}>{c.label}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {children}
          {empty && (
            <tr><td colSpan={columns.length} className="px-3 py-8 text-center text-fg-muted">Nothing here yet.</td></tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

/** A clickable table row. onClick opens the detail; action buttons in the last cell must
 * call e.stopPropagation() so they don't also trigger the row navigation. */
export function AdminRow({ onClick, children }: { onClick?: () => void; children: React.ReactNode }) {
  return (
    <tr
      onClick={onClick}
      className={`border-b border-border/40 transition-colors last:border-0 hover:bg-surface-2/40 ${onClick ? "cursor-pointer" : ""}`}
    >
      {children}
    </tr>
  );
}
