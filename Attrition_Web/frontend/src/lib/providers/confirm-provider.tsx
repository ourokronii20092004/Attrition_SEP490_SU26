"use client";

import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import { AlertTriangle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { useFocusTrap } from "@/lib/hooks/use-focus-trap";

interface ConfirmOptions {
  title?: string;
  message: string;
  confirmLabel?: string;
  danger?: boolean;
}

type ConfirmFn = (opts: ConfirmOptions) => Promise<boolean>;
const ConfirmContext = createContext<ConfirmFn | null>(null);

/**
 * Themed, accessible replacement for window.confirm — used for destructive admin actions.
 * Returns a promise that resolves true/false. Focuses the confirm button on open and closes
 * on Escape (treated as cancel).
 */
export function ConfirmProvider({ children }: { children: ReactNode }) {
  const [opts, setOpts] = useState<ConfirmOptions | null>(null);
  const resolver = useRef<((v: boolean) => void) | null>(null);

  const confirm = useCallback<ConfirmFn>((options) => {
    setOpts(options);
    return new Promise<boolean>((resolve) => { resolver.current = resolve; });
  }, []);

  const settle = useCallback((value: boolean) => {
    resolver.current?.(value);
    resolver.current = null;
    setOpts(null);
  }, []);

  const panelRef = useFocusTrap<HTMLDivElement>(!!opts);

  // Escape must cancel. A React onKeyDown on the overlay div only fires while focus is inside it,
  // so a document-level listener is what actually makes Escape work.
  useEffect(() => {
    if (!opts) return;
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") settle(false); };
    document.addEventListener("keydown", onKey);
    const prevOverflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => {
      document.removeEventListener("keydown", onKey);
      document.body.style.overflow = prevOverflow;
    };
  }, [opts, settle]);

  return (
    <ConfirmContext.Provider value={confirm}>
      {children}
      {opts && (
        <div
          className="fixed inset-0 z-[var(--z-modal)] flex items-center justify-center bg-black/80 p-4 motion-safe:animate-fade-in"
          role="alertdialog"
          aria-modal="true"
          aria-labelledby="confirm-title"
          aria-describedby="confirm-message"
          onClick={() => settle(false)}
        >
          <div ref={panelRef} className="card w-full max-w-sm p-5 shadow-[var(--shadow-lg)]" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-start gap-3">
              {opts.danger && (
                <span className="mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-danger/10 text-danger">
                  <AlertTriangle size={18} />
                </span>
              )}
              <div className="min-w-0">
                <h2 id="confirm-title" className="font-display text-lg font-semibold text-fg">
                  {opts.title ?? "Are you sure?"}
                </h2>
                <p id="confirm-message" className="mt-1 text-sm text-fg-muted">{opts.message}</p>
              </div>
            </div>
            <div className="mt-5 flex justify-end gap-2">
              {/* Cancel takes initial focus and is the safe default: for a destructive confirm,
                  landing focus on "Delete" means a reflexive Enter destroys data. */}
              <Button variant="secondary" data-autofocus onClick={() => settle(false)}>Cancel</Button>
              <Button
                variant={opts.danger ? "danger" : "primary"}
                onClick={() => settle(true)}
              >
                {opts.confirmLabel ?? "Confirm"}
              </Button>
            </div>
          </div>
        </div>
      )}
    </ConfirmContext.Provider>
  );
}

export function useConfirm(): ConfirmFn {
  const ctx = useContext(ConfirmContext);
  if (!ctx) throw new Error("useConfirm must be used within ConfirmProvider");
  return ctx;
}
