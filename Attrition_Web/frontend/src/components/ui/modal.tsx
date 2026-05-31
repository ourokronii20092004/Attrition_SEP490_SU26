"use client";

import { useEffect } from "react";
import { X } from "lucide-react";

/**
 * Generic modal dialog for admin create/edit forms. Backdrop + Escape close, focus-friendly,
 * scrollable body for tall forms. Extracted from the confirm-provider overlay pattern so admin
 * forms can move off the always-visible inline layout into a triggered modal.
 */
export function Modal({ open, onClose, title, children, size = "md" }: {
  open: boolean;
  onClose: () => void;
  title?: string;
  children: React.ReactNode;
  size?: "sm" | "md" | "lg";
}) {
  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    window.addEventListener("keydown", onKey);
    // Lock body scroll while the modal is open.
    const prev = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { window.removeEventListener("keydown", onKey); document.body.style.overflow = prev; };
  }, [open, onClose]);

  if (!open) return null;

  const maxW = size === "sm" ? "max-w-sm" : size === "lg" ? "max-w-3xl" : "max-w-xl";

  return (
    <div
      className="fixed inset-0 z-[var(--z-modal)] flex items-start justify-center overflow-y-auto bg-black/70 p-4 backdrop-blur-sm motion-safe:animate-fade-in sm:p-8"
      role="dialog"
      aria-modal="true"
      aria-label={title}
      onClick={onClose}
    >
      <div
        className={`card my-auto w-full ${maxW} p-5 shadow-[var(--shadow-lg)]`}
        onClick={(e) => e.stopPropagation()}
      >
        {title && (
          <div className="mb-4 flex items-center justify-between gap-4">
            <h2 className="font-display text-lg font-semibold text-fg">{title}</h2>
            <button onClick={onClose} aria-label="Close" className="shrink-0 text-fg-subtle transition-colors hover:text-fg">
              <X size={18} />
            </button>
          </div>
        )}
        {children}
      </div>
    </div>
  );
}
