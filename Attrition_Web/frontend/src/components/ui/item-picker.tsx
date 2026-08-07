"use client";

import { useState, useRef, useEffect, useId } from "react";
import { useQuery } from "@tanstack/react-query";
import { itemsApi } from "@/lib/api/items";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import type { ItemResponse } from "@/lib/types";

interface ItemPickerProps {
  /** Current value = itemId (matches ItemSO.itemId in the game). */
  value: string;
  label?: string;
  error?: string;
  /** onSelect passes the full item so the caller can auto-fill rarity/iconKey. */
  onSelect: (item: ItemResponse) => void;
  /** Lets a manually typed value (an itemId not yet in the DB) survive blur. */
  onManualChange?: (raw: string) => void;
}

/**
 * Item input with autocomplete suggestions from the item DB (GET /api/items?search=).
 * Replaces a plain text field: type to search, pick from the list to write the
 * itemId and auto-fill rarity/icon.
 */
export function ItemPicker({ value, label, error, onSelect, onManualChange }: ItemPickerProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState(value);
  const debounced = useDebouncedValue(query.trim(), 250);
  const rootRef = useRef<HTMLDivElement>(null);
  const inputId = useId();
  const errorId = `${inputId}-error`;

  // Keep in sync when the external value changes (e.g. loading an enemy to edit).
  useEffect(() => { setQuery(value); }, [value]);

  // Close the dropdown on an outside click.
  useEffect(() => {
    const onDown = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) setOpen(false);
    };
    document.addEventListener("mousedown", onDown);
    return () => document.removeEventListener("mousedown", onDown);
  }, []);

  const { data: items = [], isFetching } = useQuery({
    queryKey: ["item-picker", debounced],
    enabled: open,
    queryFn: async () => {
      const res = await itemsApi.list(debounced ? { search: debounced } : undefined);
      return res.success ? res.data.slice(0, 20) : [];
    },
  });

  return (
    <div className="space-y-1.5" ref={rootRef}>
      {label && (
        <label htmlFor={inputId} className="block text-xs font-medium uppercase tracking-wider text-fg-muted">{label}</label>
      )}
      <div className="relative">
        <input
          id={inputId}
          value={query}
          onChange={(e) => { setQuery(e.target.value); setOpen(true); }}
          onFocus={() => setOpen(true)}
          onBlur={() => onManualChange?.(query)}
          placeholder="Search items…"
          autoComplete="off"
          aria-invalid={!!error}
          aria-describedby={error ? errorId : undefined}
          className={
            "w-full rounded-md border border-border bg-surface-2/60 px-3.5 py-2.5 text-fg outline-none transition-colors " +
            "placeholder:text-fg-subtle focus:border-accent focus:bg-surface-2 focus:ring-1 focus:ring-accent " +
            (error ? "border-danger focus:border-danger focus:ring-danger" : "")
          }
        />
        {open && (
          <div className="absolute z-20 mt-1 max-h-60 w-full overflow-auto rounded-md border border-border bg-surface-1 shadow-lg">
            {isFetching && <div className="px-3 py-2 text-sm text-fg-subtle">Searching…</div>}
            {!isFetching && items.length === 0 && (
              <div className="px-3 py-2 text-sm text-fg-subtle">No matching items.</div>
            )}
            {items.map((it) => (
              <button
                key={it.itemId}
                type="button"
                onMouseDown={(e) => {
                  // onMouseDown (not onClick) so this fires BEFORE the input's onBlur.
                  e.preventDefault();
                  onSelect(it);
                  setQuery(it.itemId);
                  setOpen(false);
                }}
                className="flex w-full items-center justify-between gap-2 px-3 py-2 text-left text-sm hover:bg-surface-2"
              >
                <span className="text-fg">{it.name}</span>
                <span className="text-xs text-fg-subtle">{it.itemId} · {it.rarity}</span>
              </button>
            ))}
          </div>
        )}
      </div>
      {error && <p id={errorId} className="text-xs text-danger">{error}</p>}
    </div>
  );
}
