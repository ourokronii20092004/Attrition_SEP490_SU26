"use client";

import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Flag, ShieldAlert } from "lucide-react";
import { forumApi } from "@/lib/api/forum";
import { userReportsApi } from "@/lib/api/user-reports";
import { LIVE_NORMAL, liveWhenFocused } from "@/lib/live";

/**
 * Post reports and user reports are the same job — a moderation queue with a status filter,
 * resolve and dismiss — so they live behind one set of tabs instead of two sidebar entries.
 *
 * Both original routes stay live: /admin/forum/reports and /admin/user-reports each render this
 * with their own tab preset, so existing links, bookmarks and the `g f` / `g r` hotkeys all still
 * land where they did. The tabs are real <Link>s rather than local state for the same reason.
 *
 * Each tab carries its pending count so "is there anything waiting?" is answerable without
 * visiting both queues.
 */

type ReportsTab = "posts" | "users";

const TABS: { key: ReportsTab; label: string; href: string; icon: typeof Flag }[] = [
  { key: "posts", label: "Post reports", href: "/admin/forum/reports", icon: Flag },
  { key: "users", label: "User reports", href: "/admin/user-reports", icon: ShieldAlert },
];

export function ReportsWorkspace({ tab, children }: { tab: ReportsTab; children: React.ReactNode }) {
  // pageSize 1 — only totalCount is read, so don't pull 20 rows per poll just to render a badge.
  const { data: postPending } = useQuery({
    queryKey: ["admin", "reports", "pending-count", "posts"] as const,
    refetchInterval: liveWhenFocused(LIVE_NORMAL),
    queryFn: async () => {
      const res = await forumApi.getReports({ status: "Pending", page: 1, pageSize: 1 });
      return res.success ? res.data.totalCount : 0;
    },
  });

  const { data: userPending } = useQuery({
    queryKey: ["admin", "reports", "pending-count", "users"] as const,
    refetchInterval: liveWhenFocused(LIVE_NORMAL),
    queryFn: async () => {
      const res = await userReportsApi.adminList({ status: "Pending", page: 1, pageSize: 1 });
      return res.success ? res.data.totalCount : 0;
    },
  });

  const counts: Record<ReportsTab, number | undefined> = { posts: postPending, users: userPending };

  return (
    <div>
      <h1 className="font-display text-2xl font-bold text-fg">Reports</h1>
      <p className="mt-1 text-sm text-fg-muted">
        Everything awaiting moderation — reported forum posts and reported users.
      </p>

      <div className="mt-4 flex items-center gap-1 border-b border-border">
        {TABS.map(({ key, label, href, icon: Icon }) => {
          const active = key === tab;
          const count = counts[key];
          return (
            <Link
              key={key}
              href={href}
              aria-current={active ? "page" : undefined}
              className={`-mb-px inline-flex items-center gap-2 border-b-2 px-3 py-2 text-sm font-medium transition-colors ${
                active
                  ? "border-accent text-accent"
                  : "border-transparent text-fg-muted hover:border-border-strong hover:text-fg"
              }`}
            >
              <Icon size={15} className="shrink-0" />
              {label}
              {/* Badge only when something is actually pending — a "0" is noise. */}
              {!!count && (
                <span
                  className={`rounded-full px-1.5 py-0.5 text-[11px] font-semibold tabular-nums ${
                    active ? "bg-accent text-accent-fg" : "bg-surface-3 text-fg-muted"
                  }`}
                >
                  {count}
                </span>
              )}
            </Link>
          );
        })}
      </div>

      <div className="mt-4">{children}</div>
    </div>
  );
}
