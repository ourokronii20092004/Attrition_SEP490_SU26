"use client";

import { useState, useRef, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { itemsApi } from "@/lib/api/items";
import { useDebouncedValue } from "@/lib/hooks/use-debounced-value";
import type { ItemResponse } from "@/lib/types";

interface ItemPickerProps {
  /** Giá trị hiện tại = itemId (khớp ItemSO.itemId trong game). */
  value: string;
  label?: string;
  error?: string;
  /** onSelect trả cả item để caller auto-fill rarity/iconKey. */
  onSelect: (item: ItemResponse) => void;
  /** Cho phép giữ giá trị gõ tay (itemId cũ chưa có trong DB) khi blur. */
  onManualChange?: (raw: string) => void;
}

/**
 * Ô chọn item có gợi ý (autocomplete) từ item DB (GET /api/items?search=).
 * Thay ô text dán tay: gõ để tìm, chọn từ danh sách → ghi itemId + auto-fill rarity/icon.
 */
export function ItemPicker({ value, label, error, onSelect, onManualChange }: ItemPickerProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState(value);
  const debounced = useDebouncedValue(query.trim(), 250);
  const rootRef = useRef<HTMLDivElement>(null);

  // Đồng bộ khi giá trị ngoài đổi (vd load enemy để sửa).
  useEffect(() => { setQuery(value); }, [value]);

  // Đóng dropdown khi click ra ngoài.
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
        <label className="block text-xs font-medium uppercase tracking-wider text-fg-muted">{label}</label>
      )}
      <div className="relative">
        <input
          value={query}
          onChange={(e) => { setQuery(e.target.value); setOpen(true); }}
          onFocus={() => setOpen(true)}
          onBlur={() => onManualChange?.(query)}
          placeholder="Tìm item…"
          autoComplete="off"
          className={
            "w-full rounded-md border border-border bg-surface-2/60 px-3.5 py-2.5 text-fg outline-none transition-colors " +
            "placeholder:text-fg-subtle focus:border-accent focus:bg-surface-2 focus:ring-1 focus:ring-accent " +
            (error ? "border-danger focus:border-danger focus:ring-danger" : "")
          }
        />
        {open && (
          <div className="absolute z-20 mt-1 max-h-60 w-full overflow-auto rounded-md border border-border bg-surface-1 shadow-lg">
            {isFetching && <div className="px-3 py-2 text-sm text-fg-subtle">Đang tìm…</div>}
            {!isFetching && items.length === 0 && (
              <div className="px-3 py-2 text-sm text-fg-subtle">Không có item khớp.</div>
            )}
            {items.map((it) => (
              <button
                key={it.itemId}
                type="button"
                onMouseDown={(e) => {
                  // onMouseDown (không phải onClick) để chạy TRƯỚC onBlur của input.
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
      {error && <p className="text-xs text-danger">{error}</p>}
    </div>
  );
}
