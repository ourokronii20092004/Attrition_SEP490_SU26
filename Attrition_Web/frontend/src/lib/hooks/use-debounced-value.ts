"use client";

import { useEffect, useState } from "react";

/** Returns a copy of `value` that only updates after `delayMs` of no changes.
 * Used to debounce search/filter inputs so we don't fire a query on every keystroke. */
export function useDebouncedValue<T>(value: T, delayMs = 300): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delayMs);
    return () => clearTimeout(t);
  }, [value, delayMs]);
  return debounced;
}
