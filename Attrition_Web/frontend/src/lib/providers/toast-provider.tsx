"use client";

import { createContext, useCallback, useContext, useState, type ReactNode } from "react";
import { CheckCircle2, AlertTriangle, Info, X } from "lucide-react";

type ToastKind = "success" | "error" | "info";
interface Toast { id: number; kind: ToastKind; message: string; }

interface ToastApi {
  toast: (message: string, kind?: ToastKind) => void;
}

const ToastContext = createContext<ToastApi | null>(null);

let nextId = 1;

const ICONS = { success: CheckCircle2, error: AlertTriangle, info: Info } as const;
const TONE = {
  success: "border-success/30 text-success",
  error: "border-danger/30 text-danger",
  info: "border-info/30 text-info",
} as const;

/**
 * App-wide toast notifications. The container is an aria-live region so screen readers
 * announce async results (post created, save failed) that previously used window.alert or
 * silent banners. Errors are assertive; success/info are polite.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const remove = useCallback((id: number) => setToasts((t) => t.filter((x) => x.id !== id)), []);

  const toast = useCallback((message: string, kind: ToastKind = "info") => {
    const id = nextId++;
    setToasts((t) => [...t, { id, kind, message }]);
    // Auto-dismiss; errors linger longer so they're not missed.
    setTimeout(() => remove(id), kind === "error" ? 6000 : 4000);
  }, [remove]);

  return (
    <ToastContext.Provider value={{ toast }}>
      {children}
      <div
        className="pointer-events-none fixed bottom-4 right-4 z-[var(--z-toast)] flex w-[min(92vw,22rem)] flex-col gap-2"
        role="region"
        aria-label="Notifications"
      >
        {toasts.map((t) => {
          const Icon = ICONS[t.kind];
          return (
            <div
              key={t.id}
              role={t.kind === "error" ? "alert" : "status"}
              aria-live={t.kind === "error" ? "assertive" : "polite"}
              className={`glass pointer-events-auto flex items-start gap-3 rounded-card border px-4 py-3 text-sm shadow-[var(--shadow-md)] motion-safe:animate-rise-in ${TONE[t.kind]}`}
            >
              <Icon size={18} className="mt-0.5 shrink-0" />
              <p className="flex-1 text-fg">{t.message}</p>
              <button onClick={() => remove(t.id)} aria-label="Dismiss" className="shrink-0 text-fg-subtle transition-colors hover:text-fg">
                <X size={16} />
              </button>
            </div>
          );
        })}
      </div>
    </ToastContext.Provider>
  );
}

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext);
  if (!ctx) throw new Error("useToast must be used within ToastProvider");
  return ctx;
}
