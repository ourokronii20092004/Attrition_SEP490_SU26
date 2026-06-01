"use client";

import { useState } from "react";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Bell, CheckCheck } from "lucide-react";
import { notificationsApi } from "@/lib/api/notifications";
import { useAuth } from "@/lib/providers";
import { RelativeTime } from "@/components/ui/relative-time";

/**
 * Header notification bell. Polls the unread count every 30s while logged in; opening the
 * dropdown fetches the list. Clicking a notification marks it read and navigates to its link.
 */
export function NotificationBell() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);

  const { data: unread = 0 } = useQuery({
    queryKey: ["notifications", "unread"],
    enabled: !!user,
    refetchInterval: 30_000,
    queryFn: async () => {
      const res = await notificationsApi.unreadCount();
      return res.success ? res.data : 0;
    },
  });

  const { data: items = [] } = useQuery({
    queryKey: ["notifications", "list"],
    enabled: !!user && open, // only fetch the list when the dropdown is open
    queryFn: async () => {
      const res = await notificationsApi.list(20);
      return res.success ? res.data : [];
    },
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ["notifications", "unread"] });
    queryClient.invalidateQueries({ queryKey: ["notifications", "list"] });
  };

  const markRead = useMutation({
    mutationFn: async (id: string) => { await notificationsApi.markRead(id); },
    onSuccess: refresh,
  });
  const markAll = useMutation({
    mutationFn: async () => { await notificationsApi.markAllRead(); },
    onSuccess: refresh,
  });

  if (!user) return null;

  return (
    <div className="relative">
      <button
        onClick={() => setOpen((v) => !v)}
        className="relative flex h-9 w-9 items-center justify-center rounded-md text-fg-muted transition-colors hover:bg-surface-2 hover:text-fg"
        aria-label={`Notifications${unread > 0 ? ` (${unread} unread)` : ""}`}
      >
        <Bell size={18} />
        {unread > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex min-w-4 items-center justify-center rounded-full bg-accent px-1 text-[10px] font-semibold text-accent-fg">
            {unread > 9 ? "9+" : unread}
          </span>
        )}
      </button>

      {open && (
        <>
          <div className="fixed inset-0 z-[var(--z-dropdown)]" onClick={() => setOpen(false)} />
          <div className="absolute right-0 z-[var(--z-dropdown)] mt-2 w-80 overflow-hidden rounded-card border border-border bg-surface shadow-[var(--shadow-lg)]">
            <div className="flex items-center justify-between border-b border-border px-3 py-2">
              <span className="text-sm font-medium text-fg">Notifications</span>
              {items.some((n) => !n.isRead) && (
                <button onClick={() => markAll.mutate()} className="inline-flex items-center gap-1 text-xs text-fg-subtle hover:text-accent">
                  <CheckCheck size={13} /> Mark all read
                </button>
              )}
            </div>
            <div className="max-h-96 overflow-y-auto">
              {items.length === 0 ? (
                <p className="px-3 py-8 text-center text-sm text-fg-muted">No notifications.</p>
              ) : (
                items.map((n) => {
                  const body = (
                    <div className={`flex flex-col gap-0.5 px-3 py-2.5 transition-colors hover:bg-surface-2 ${n.isRead ? "" : "bg-accent-soft/30"}`}>
                      <span className="text-sm text-fg">{n.message}</span>
                      <span className="text-xs text-fg-subtle"><RelativeTime iso={n.createdAt} /></span>
                    </div>
                  );
                  const onClick = () => { if (!n.isRead) markRead.mutate(n.id); setOpen(false); };
                  return n.link ? (
                    <Link key={n.id} href={n.link} onClick={onClick} className="block border-b border-border/50 last:border-0">{body}</Link>
                  ) : (
                    <button key={n.id} onClick={onClick} className="block w-full border-b border-border/50 text-left last:border-0">{body}</button>
                  );
                })
              )}
            </div>
          </div>
        </>
      )}
    </div>
  );
}
