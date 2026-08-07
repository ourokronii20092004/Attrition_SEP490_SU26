"use client";

import { useEffect, useRef } from "react";

const FOCUSABLE = [
  "a[href]",
  "button:not([disabled])",
  "input:not([disabled]):not([type='hidden'])",
  "select:not([disabled])",
  "textarea:not([disabled])",
  "[tabindex]:not([tabindex='-1'])",
].join(",");

/**
 * Focus management for modal dialogs.
 *
 * A dialog that traps neither focus nor Tab leaves keyboard and screen-reader users stranded:
 * focus stays on the page behind the overlay, so Tab walks through content they can't see and
 * Enter can activate a control the dialog is covering. This hook does the three things an
 * accessible dialog owes the user:
 *
 *   1. moves focus into the dialog on open (first focusable, or the container itself),
 *   2. keeps Tab / Shift+Tab cycling inside it while open,
 *   3. returns focus to whatever was focused before, on close.
 *
 * Attach the returned ref to the dialog's content element (the panel, not the backdrop).
 */
export function useFocusTrap<T extends HTMLElement = HTMLDivElement>(active: boolean) {
  const ref = useRef<T | null>(null);
  const restoreTo = useRef<HTMLElement | null>(null);

  useEffect(() => {
    if (!active) return;
    const node = ref.current;
    if (!node) return;

    // Remember the trigger so focus can go home when the dialog closes.
    restoreTo.current = document.activeElement instanceof HTMLElement ? document.activeElement : null;

    const focusables = () =>
      Array.from(node.querySelectorAll<HTMLElement>(FOCUSABLE)).filter(
        (el) => el.offsetWidth > 0 || el.offsetHeight > 0 || el === document.activeElement,
      );

    // Move focus in. Prefer an explicitly marked element, then the first natural stop, then the
    // panel itself (made programmatically focusable) so focus is never left outside.
    const preferred = node.querySelector<HTMLElement>("[data-autofocus]");
    const first = preferred ?? focusables()[0];
    if (first) {
      first.focus();
    } else {
      node.setAttribute("tabindex", "-1");
      node.focus();
    }

    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key !== "Tab") return;
      const items = focusables();
      if (items.length === 0) {
        // Nothing to tab to — keep focus pinned to the panel rather than letting it escape.
        e.preventDefault();
        return;
      }
      const firstItem = items[0];
      const lastItem = items[items.length - 1];
      const current = document.activeElement as HTMLElement | null;

      if (e.shiftKey) {
        if (current === firstItem || !node.contains(current)) {
          e.preventDefault();
          lastItem.focus();
        }
      } else if (current === lastItem || !node.contains(current)) {
        e.preventDefault();
        firstItem.focus();
      }
    };

    document.addEventListener("keydown", onKeyDown, true);
    return () => {
      document.removeEventListener("keydown", onKeyDown, true);
      // Only restore if focus is still inside the dialog (or nowhere); if the user has already
      // clicked elsewhere, don't yank it back.
      const active_ = document.activeElement;
      const stillInside = !active_ || active_ === document.body || node.contains(active_);
      if (stillInside) restoreTo.current?.focus?.();
    };
  }, [active]);

  return ref;
}
