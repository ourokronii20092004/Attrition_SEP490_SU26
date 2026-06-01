"use client";

import { useCallback, useEffect } from "react";
import { X } from "lucide-react";
import { useConfirm } from "@/lib/providers";

/**
 * Generic modal dialog for admin create/edit forms. Backdrop + Escape close, focus-friendly,
 * scrollable body for tall forms. Extracted from the confirm-provider overlay pattern so admin
 * forms can move off the always-visible inline layout into a triggered modal.
 *
 * QOLF-6: pass `dirty` when the modal holds unsaved form input. A backdrop click or Escape then
 * routes through a "Discard your changes?" confirm instead of closing immediately, so a stray
 * click outside can't wipe filled-in data. The X button and explicit saves bypass the guard via
 * onClose directly (they're intentional). Programmatic closes (e.g. after save) should set
 * dirty=false first, or call onClose — they aren't intercepted.
 */
export function Modal({ open, onClose, title, children, size = "md", dirty = false }: {
  open: boolean;
  onClose: () => void;
  title?: string;
  children: React.ReactNode;
  size?: "sm" | "md" | "lg";
  dirty?: boolean;
}) {
  const confirm = useConfirm();

  // Guarded close: when the form is dirty, confirm before discarding (QOLF-6).
  const attemptClose = useCallback(async () => {
    if (!dirty) { onClose(); return; }
    const ok = await confirm({
      title: "Discard your changes?",
      message: "You have unsaved changes. If you leave now, they'll be lost.",
      confirmLabel: "Discard",
      danger: true,
    });
    if (ok) onClose();
  }, [dirty, confirm, onClose]);

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") attemptClose(); };
    window.addEventListener("keydown", onKey);
    // Lock body scroll while the modal is open.
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { window.removeEventListener("keydown", onKey); document.body.style.overflow = prev; };
  }, [open, attemptClose]);

  if (!open) return null;

  const maxW = size === "sm" ? "max-w-sm" : size === "lg" ? "max-w-3xl" : "max-w-xl";

  return (
    <div
      className="fixed inset-0 z-[var(--z-modal)] flex items-start justify-center overflow-y-auto bg-black/70 p-4 backdrop-blur-sm motion-safe:animate-fade-in sm:p-8"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onClick={attemptClose}
    >
      <div
        className={`card my-auto w-full ${maxW} p-5 shadow-[var(--shadow-lg)]`}
        onClick={(e) => e.stopPropagation()}
      >
        {title && (
          <div className="mb-4 flex items-center justify-between gap-4">
            <h2 className="font-display text-lg font-semibold text-fg">{title}</h2>
            <button onClick={attemptClose} aria-label="Close" className="shrink-0 text-fg-subtle transition-colors hover:text-fg">
              <X size={18} />
            </button>
          </div>
        )}
        {children}
      </div>
    </div>
  );
}
