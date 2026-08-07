"use client";

import Link from "next/link";
import { Search, Plus, ArrowUp, ArrowDown, ChevronsUpDown } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

/**
 * Shared admin table-page primitives (the standard format for every admin list page):
 *   1. AdminPageHeader  — page title + a single Add button, on one line (no wasted row).
 *   2. AdminFilterBar   — a search box followed by a row of dropdown filters.
 *   3. AdminTable       — header + clickable rows; row click opens detail, edit/delete sit on the side.
 *
 * These exist so admin pages are uniform and dense, not each hand-rolled differently.
 *
 * Density is the point here, but density without sorting, sticky headers or bulk actions is just
 * cramped. Columns can opt into sorting, the header row sticks while the body scrolls, and pages
 * that need multi-select get it from `selection` — all additive, so existing call sites are
 * unaffected if they pass none of it.
 */

export function AdminPageHeader({ title, addLabel, onAdd, addHref, children }: {
  title: string;
  addLabel?: string;
  onAdd?: () => void;
  addHref?: string;
  /** Extra actions rendered left of the Add button (e.g. a bulk-action bar). */
  children?: React.ReactNode;
}) {
  const addBtn = addLabel ? (
    <Button size="sm" onClick={onAdd}><Plus size={15} className="mr-1.5" />{addLabel}</Button>
  ) : null;
  return (
    <div className="flex items-center justify-between gap-4">
      <h1 className="font-display text-2xl font-bold text-fg">{title}</h1>
      <div className="flex items-center gap-2">
        {children}
        {addHref && addLabel ? <Link href={addHref}>{addBtn}</Link> : addBtn}
      </div>
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
            aria-label={searchPlaceholder}
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

export type AdminColumn = {
  key: string;
  label: string;
  align?: "left" | "right";
  /** Mark sortable to get a clickable header. The page owns the actual comparison. */
  sortable?: boolean;
};

export type SortState = { key: string; dir: "asc" | "desc" } | null;

export type SelectionState = {
  /** Ids currently ticked. */
  selected: Set<string>;
  onChange: (next: Set<string>) => void;
  /** Every id on the current page — powers the header select-all checkbox. */
  pageIds: string[];
};

/** Table shell: pass column headers and rows. Rows are clickable when onRowClick is given;
 * put edit/delete buttons in the last cell (they should stopPropagation). */
export function AdminTable({ columns, children, empty, emptyLabel, emptyHint, loading, sort, onSortChange, selection }: {
  columns: AdminColumn[];
  children: React.ReactNode;
  empty?: boolean;
  /** Headline for the empty row. Defaults to the old "Nothing here yet." */
  emptyLabel?: string;
  /** Second line — usually "clear the filters" vs "create the first one". */
  emptyHint?: string;
  /** Render placeholder rows inside the real table instead of blanking the whole page. */
  loading?: boolean;
  sort?: SortState;
  onSortChange?: (next: SortState) => void;
  selection?: SelectionState;
}) {
  const allSelected =
    !!selection && selection.pageIds.length > 0 && selection.pageIds.every((id) => selection.selected.has(id));
  const someSelected = !!selection && selection.pageIds.some((id) => selection.selected.has(id)) && !allSelected;

  const toggleAll = () => {
    if (!selection) return;
    const next = new Set(selection.selected);
    if (allSelected) {
      selection.pageIds.forEach((id) => next.delete(id));
    } else {
      selection.pageIds.forEach((id) => next.add(id));
    }
    selection.onChange(next);
  };

  const headerFor = (c: AdminColumn) => {
    if (!c.sortable || !onSortChange) return c.label;
    const active = sort?.key === c.key;
    const nextDir = active && sort?.dir === "asc" ? "desc" : "asc";
    const Icon = !active ? ChevronsUpDown : sort?.dir === "asc" ? ArrowUp : ArrowDown;
    return (
      <button
        type="button"
        onClick={() => onSortChange({ key: c.key, dir: nextDir })}
        className={`-mx-1 inline-flex items-center gap-1 rounded px-1 py-0.5 transition-colors hover:text-fg ${
          active ? "text-accent" : ""
        } ${c.align === "right" ? "flex-row-reverse" : ""}`}
        title={`Sort by ${c.label}`}
      >
        {c.label}
        <Icon size={12} className={active ? "" : "opacity-50"} />
      </button>
    );
  };

  const colCount = columns.length + (selection ? 1 : 0);

  return (
    <div className="mt-4 overflow-x-auto rounded-lg border border-border">
      <table className="w-full text-sm">
        <thead>
          {/* Sticky so column meaning survives scrolling a full page of rows. */}
          <tr className="sticky top-0 z-10 border-b border-border bg-surface-2 text-left text-xs uppercase tracking-wider text-fg-subtle">
            {selection && (
              <th scope="col" className="w-9 px-3 py-2">
                <input
                  type="checkbox"
                  checked={allSelected}
                  ref={(el) => { if (el) el.indeterminate = someSelected; }}
                  onChange={toggleAll}
                  aria-label={allSelected ? "Clear selection" : "Select all rows on this page"}
                  className="rounded border-border"
                />
              </th>
            )}
            {columns.map((c) => (
              <th
                key={c.key}
                scope="col"
                aria-sort={sort?.key === c.key ? (sort.dir === "asc" ? "ascending" : "descending") : undefined}
                className={`px-3 py-2 font-medium ${c.align === "right" ? "text-right" : ""}`}
              >
                {headerFor(c)}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {loading ? (
            Array.from({ length: 8 }).map((_, r) => (
              <tr key={r} className="border-b border-border/40 last:border-0">
                {Array.from({ length: colCount }).map((__, c) => (
                  <td key={c} className="px-3 py-2.5"><Skeleton className="h-4 w-full" /></td>
                ))}
              </tr>
            ))
          ) : (
            <>
              {children}
              {empty && (
                <tr>
                  <td colSpan={colCount} className="px-3 py-10 text-center">
                    <p className="font-medium text-fg">{emptyLabel ?? "Nothing here yet."}</p>
                    {emptyHint && <p className="mt-1 text-xs text-fg-muted">{emptyHint}</p>}
                  </td>
                </tr>
              )}
            </>
          )}
        </tbody>
      </table>
    </div>
  );
}

/** A clickable table row. onClick opens the detail; action buttons in the last cell must
 * call e.stopPropagation() so they don't also trigger the row navigation.
 *
 * A bare onClick on <tr> is mouse-only, so the row also takes keyboard focus and responds to
 * Enter/Space when it's interactive. */
export function AdminRow({ onClick, children, selected }: {
  onClick?: () => void;
  children: React.ReactNode;
  /** Tints the row while it's ticked in a bulk selection. */
  selected?: boolean;
}) {
  return (
    <tr
      onClick={onClick}
      onKeyDown={
        onClick
          ? (e) => {
              // Ignore keys forwarded from a control inside the row (buttons handle their own).
              if (e.target !== e.currentTarget) return;
              if (e.key === "Enter" || e.key === " ") { e.preventDefault(); onClick(); }
            }
          : undefined
      }
      tabIndex={onClick ? 0 : undefined}
      role={onClick ? "button" : undefined}
      className={`border-b border-border/40 transition-colors last:border-0 focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-accent ${
        selected ? "bg-accent-soft/40" : "hover:bg-surface-2/40"
      } ${onClick ? "cursor-pointer" : ""}`}
    >
      {children}
    </tr>
  );
}

/** Checkbox cell for a selectable row. Stops propagation so ticking never opens the detail. */
export function AdminSelectCell({ id, selection }: { id: string; selection: SelectionState }) {
  const checked = selection.selected.has(id);
  return (
    <td className="px-3 py-2" onClick={(e) => e.stopPropagation()}>
      <input
        type="checkbox"
        checked={checked}
        onChange={() => {
          const next = new Set(selection.selected);
          if (checked) next.delete(id); else next.add(id);
          selection.onChange(next);
        }}
        aria-label={checked ? "Deselect row" : "Select row"}
        className="rounded border-border"
      />
    </td>
  );
}

/** Bar that appears once rows are ticked: count + the actions that apply to them. */
export function AdminBulkBar({ count, onClear, children }: {
  count: number;
  onClear: () => void;
  children: React.ReactNode;
}) {
  if (count === 0) return null;
  return (
    <div className="flex items-center gap-2 rounded-md border border-accent/40 bg-accent-soft px-2.5 py-1">
      <span className="text-xs font-medium tabular-nums text-accent">{count} selected</span>
      {children}
      <button
        onClick={onClear}
        className="rounded px-1.5 py-0.5 text-xs text-fg-muted transition-colors hover:text-fg"
      >
        Clear
      </button>
    </div>
  );
}

/** Sort a list by the active SortState using a per-column value accessor. */
export function applySort<T>(rows: T[], sort: SortState, accessors: Record<string, (row: T) => string | number | null | undefined>): T[] {
  if (!sort) return rows;
  const get = accessors[sort.key];
  if (!get) return rows;
  const dir = sort.dir === "asc" ? 1 : -1;
  // Copy first: sorting the query cache's array in place makes React Query's data mutate underneath
  // components that are still rendering it.
  return [...rows].sort((a, b) => {
    const av = get(a), bv = get(b);
    if (av == null && bv == null) return 0;
    if (av == null) return 1;   // blanks sink regardless of direction
    if (bv == null) return -1;
    if (typeof av === "number" && typeof bv === "number") return (av - bv) * dir;
    return String(av).localeCompare(String(bv), undefined, { numeric: true, sensitivity: "base" }) * dir;
  });
}
