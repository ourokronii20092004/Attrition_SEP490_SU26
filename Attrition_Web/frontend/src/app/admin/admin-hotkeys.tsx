"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Keyboard, X } from "lucide-react";

/**
 * Admin keyboard shortcuts. Each top-level destination is reachable with a "g then <key>" chord
 * (press g, then the key within ~1.2s) — a common pattern (Gmail/GitHub) that avoids clobbering
 * browser/OS Alt/Ctrl combos. "?" opens the cheat-sheet. Shortcuts are suppressed while typing in
 * an input/textarea/select or with a modifier held.
 */
export const ADMIN_SHORTCUTS: { keys: string; label: string; href: string }[] = [
  { keys: "g d", label: "Dashboard", href: "/admin" },
  { keys: "g u", label: "Users", href: "/admin/users" },
  { keys: "g r", label: "User Reports", href: "/admin/user-reports" },
  { keys: "g w", label: "Wiki Articles", href: "/admin/wiki/articles" },
  { keys: "g f", label: "Forum Reports", href: "/admin/forum/reports" },
  { keys: "g t", label: "Forum Threads", href: "/admin/forum/threads" },
  { keys: "g e", label: "Enemies", href: "/admin/enemies" },
  { keys: "g i", label: "Items", href: "/admin/items" },
  { keys: "g a", label: "Assets", href: "/admin/assets" },
  { keys: "g m", label: "Music Tracks", href: "/admin/music/tracks" },
  { keys: "g c", label: "Characters", href: "/admin/characters" },
];

const SECOND_KEY: Record<string, string> = Object.fromEntries(
  ADMIN_SHORTCUTS.map((s) => [s.keys.split(" ")[1], s.href])
);

function isTyping(el: EventTarget | null): boolean {
  const node = el as HTMLElement | null;
  if (!node) return false;
  const tag = node.tagName;
  return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT" || node.isContentEditable;
}

/** Custom event other components (e.g. the top-bar hint button) fire to open the cheat-sheet. */
export const ADMIN_HELP_EVENT = "attrition:admin:help";

export function AdminHotkeys() {
  const router = useRouter();
  const [helpOpen, setHelpOpen] = useState(false);

  useEffect(() => {
    const open = () => setHelpOpen(true);
    window.addEventListener(ADMIN_HELP_EVENT, open);
    return () => window.removeEventListener(ADMIN_HELP_EVENT, open);
  }, []);

  useEffect(() => {
    let awaitingG = false;
    let timer: ReturnType<typeof setTimeout> | undefined;

    const clearG = () => { awaitingG = false; if (timer) clearTimeout(timer); };

    const onKey = (e: KeyboardEvent) => {
      if (e.metaKey || e.ctrlKey || e.altKey || isTyping(e.target)) return;
      const key = e.key.toLowerCase();

      if (key === "?") { e.preventDefault(); setHelpOpen((o) => !o); return; }
      if (key === "escape") { clearG(); setHelpOpen(false); return; }

      if (awaitingG) {
        const href = SECOND_KEY[key];
        clearG();
        if (href) { e.preventDefault(); router.push(href); }
        return;
      }
      if (key === "g") {
        awaitingG = true;
        timer = setTimeout(() => { awaitingG = false; }, 1200);
      }
    };

    window.addEventListener("keydown", onKey);
    return () => { window.removeEventListener("keydown", onKey); if (timer) clearTimeout(timer); };
  }, [router]);

  if (!helpOpen) return null;

  return (
    <div
      className="fixed inset-0 z-[var(--z-modal)] flex items-center justify-center bg-black/80 p-4 motion-safe:animate-fade-in"
      role="dialog"
      aria-modal="true"
      aria-label="Keyboard shortcuts"
      onClick={() => setHelpOpen(false)}
    >
      <div className="card w-full max-w-md p-5 shadow-[var(--shadow-lg)]" onClick={(e) => e.stopPropagation()}>
        <div className="mb-4 flex items-center justify-between">
          <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-fg">
            <Keyboard size={18} /> Keyboard shortcuts
          </h2>
          <button onClick={() => setHelpOpen(false)} aria-label="Close" className="text-fg-subtle transition-colors hover:text-fg">
            <X size={18} />
          </button>
        </div>
        <p className="mb-3 text-xs text-fg-muted">Press <Kbd>g</Kbd> then the key. <Kbd>⌘K</Kbd> opens search.</p>
        <ul className="space-y-1.5">
          {ADMIN_SHORTCUTS.map((s) => (
            <li key={s.href} className="flex items-center justify-between text-sm">
              <span className="text-fg">{s.label}</span>
              <span className="flex gap-1">
                {s.keys.split(" ").map((k, i) => <Kbd key={i}>{k}</Kbd>)}
              </span>
            </li>
          ))}
          <li className="flex items-center justify-between border-t border-border pt-1.5 text-sm">
            <span className="text-fg">Toggle this help</span>
            <Kbd>?</Kbd>
          </li>
        </ul>
      </div>
    </div>
  );
}

function Kbd({ children }: { children: React.ReactNode }) {
  return (
    <kbd className="inline-flex h-5 min-w-5 items-center justify-center rounded border border-border bg-surface-2 px-1.5 text-[11px] font-medium text-fg-muted">
      {children}
    </kbd>
  );
}
