"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ChevronRight, Clock } from "lucide-react";
import { adminLabelFor } from "./admin-routes";

const LAST_KEY = "attrition:admin:lastPage";
const RECENT_KEY = "attrition:admin:recent";
const MAX_RECENT = 6;

/** Read the last-visited admin page (used by the layout to offer "resume"). */
export function getLastAdminPage(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(LAST_KEY);
}

/**
 * File-manager-style breadcrumb + recently-visited chips for the admin shell.
 * Records each visit to localStorage, powering the recent list and resume-last-page.
 */
export function AdminTopBar() {
  const pathname = usePathname();
  const [recent, setRecent] = useState<string[]>([]);

  useEffect(() => {
    if (!pathname.startsWith("/admin")) return;
    localStorage.setItem(LAST_KEY, pathname);
    const prev: string[] = JSON.parse(localStorage.getItem(RECENT_KEY) || "[]");
    const next = [pathname, ...prev.filter((p) => p !== pathname)].slice(0, MAX_RECENT);
    localStorage.setItem(RECENT_KEY, JSON.stringify(next));
    setRecent(next);
  }, [pathname]);

  // Breadcrumb segments: always start at Dashboard, then the current page (if deeper).
  const crumbs: { href: string; label: string }[] = [{ href: "/admin", label: "Dashboard" }];
  if (pathname !== "/admin") crumbs.push({ href: pathname, label: adminLabelFor(pathname) });

  const recentOthers = recent.filter((p) => p !== pathname).slice(0, 4);

  return (
    <div className="sticky top-0 z-[150] flex flex-wrap items-center justify-between gap-3 border-b border-border bg-surface/80 px-4 py-2.5 backdrop-blur sm:px-6 lg:px-8">
      <nav aria-label="Breadcrumb" className="flex items-center gap-1 text-sm">
        {crumbs.map((c, i) => (
          <span key={c.href} className="flex items-center gap-1">
            {i > 0 && <ChevronRight size={14} className="text-fg-subtle" />}
            {i === crumbs.length - 1 ? (
              <span className="font-medium text-fg">{c.label}</span>
            ) : (
              <Link href={c.href} className="text-fg-muted transition-colors hover:text-accent">{c.label}</Link>
            )}
          </span>
        ))}
      </nav>

      {recentOthers.length > 0 && (
        <div className="flex items-center gap-1.5 overflow-x-auto">
          <Clock size={13} className="shrink-0 text-fg-subtle" />
          {recentOthers.map((p) => (
            <Link
              key={p}
              href={p}
              className="shrink-0 rounded-full border border-border px-2.5 py-0.5 text-xs text-fg-muted transition-colors hover:border-accent/50 hover:text-fg"
            >
              {adminLabelFor(p)}
            </Link>
          ))}
        </div>
      )}
    </div>
  );
}
