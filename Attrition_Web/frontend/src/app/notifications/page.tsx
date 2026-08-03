"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery, useMutation, useQueryClient, keepPreviousData } from "@tanstack/react-query";
import { Bell, BellOff, CheckCheck, MessageSquare, AtSign, Megaphone } from "lucide-react";
import { notificationsApi } from "@/lib/api/notifications";
import { forumApi } from "@/lib/api/forum";
import { useAuth, useToast } from "@/lib/providers";
import { threadIdFromNotificationLink } from "@/lib/notification-link";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Select } from "@/components/ui/select";
import { Pagination } from "@/components/ui/pagination";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { RelativeTime } from "@/components/ui/relative-time";
import type { NotificationDto } from "@/lib/types";
import { LIVE_FAST, liveWhenFocused } from "@/lib/live";
import { useLoginHref } from "@/lib/hooks/use-login-href";

const PAGE_SIZE = 20;

// Icon per notification type so the history scans quickly. Falls back to a generic bell.
function typeIcon(type: string) {
  switch (type.toLowerCase()) {
    case "reply": return MessageSquare;
    case "mention": return AtSign;
    case "announcement": return Megaphone;
    default: return Bell;
  }
}

export default function NotificationsPage() {
  const loginHref = useLoginHref();
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();
  const queryClient = useQueryClient();

  const [page, setPage] = useState(1);
  const [filter, setFilter] = useState<"all" | "unread">("all");

  useEffect(() => {
    if (authLoading) return;
    if (!user) router.push(loginHref);
  }, [user, authLoading, router]);

  const { data, isPending } = useQuery({
    queryKey: ["notifications", "paged", page, filter],
    // The full notification list, kept in step with the bell.
    refetchInterval: liveWhenFocused(LIVE_FAST),
    enabled: !!user && !authLoading,
    placeholderData: keepPreviousData,
    queryFn: async () => {
      const res = await notificationsApi.listPaged({ page, pageSize: PAGE_SIZE, unreadOnly: filter === "unread" });
      return res.success ? res.data : null;
    },
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: ["notifications"] });
  };

  const markRead = useMutation({
    mutationFn: async (id: string) => { await notificationsApi.markRead(id); },
    onSuccess: refresh,
  });
  const markAll = useMutation({
    mutationFn: async () => { await notificationsApi.markAllRead(); },
    onSuccess: refresh,
  });

  if (!user && !authLoading) return null;

  const items = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;
  const hasUnread = items.some((n) => !n.isRead);

  return (
    <PageShell size="lg">
      <PageTitle description="Everything that's pinged you — replies, mentions, and announcements.">
        Notifications
      </PageTitle>

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="w-44">
          <Select
            value={filter}
            onChange={(e) => { setFilter(e.target.value as "all" | "unread"); setPage(1); }}
            aria-label="Filter notifications"
          >
            <option value="all">All notifications</option>
            <option value="unread">Unread only</option>
          </Select>
        </div>
        {hasUnread && (
          <button
            onClick={() => markAll.mutate()}
            disabled={markAll.isPending}
            className="inline-flex items-center gap-1.5 rounded-md border border-border px-3 py-2 text-sm text-fg-muted transition-colors hover:border-accent hover:text-accent disabled:opacity-50"
          >
            <CheckCheck size={15} /> Mark all read
          </button>
        )}
      </div>

      <div className="mt-6">
        {isPending ? (
          <SkeletonList rows={6} />
        ) : items.length === 0 ? (
          <EmptyState
            icon={Bell}
            title={filter === "unread" ? "No unread notifications" : "No notifications yet"}
            description={filter === "unread" ? "You're all caught up." : "Replies and mentions will show up here."}
          />
        ) : (
          <ul className="stagger space-y-2">
            {items.map((n, i) => (
              <li key={n.id} style={{ "--i": i } as React.CSSProperties}>
                <NotificationRow notification={n} onMarkRead={() => markRead.mutate(n.id)} />
              </li>
            ))}
          </ul>
        )}
      </div>

      <Pagination page={page} totalPages={totalPages} onChange={setPage} />
    </PageShell>
  );
}

function NotificationRow({ notification: n, onMarkRead }: { notification: NotificationDto; onMarkRead: () => void }) {
  const Icon = typeIcon(n.type);
  const { toast } = useToast();
  const queryClient = useQueryClient();
  // Only reply notifications carry a thread link; a mention elsewhere has nothing to mute.
  const threadId = threadIdFromNotificationLink(n.link);

  const mute = useMutation({
    // Mute the thread and clear the pile it already produced — stopping new notifications while
    // leaving the old ones sitting unread would only half-solve the annoyance.
    mutationFn: async (id: string) => {
      await forumApi.setThreadMuted(id, true);
      await notificationsApi.markThreadRead(id);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["notifications"] });
      toast("Muted. You won't get notifications from that thread.", "success");
    },
    onError: () => toast("Couldn't mute that thread. Please try again.", "error"),
  });

  const body = (
    <div
      className={`flex items-start gap-3 rounded-card border p-4 transition-colors ${
        n.isRead
          ? "border-border bg-surface hover:bg-surface-2"
          : "border-accent/40 bg-accent-soft/30 hover:bg-accent-soft/50"
      }`}
    >
      <span
        className={`mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-full ${
          n.isRead ? "bg-surface-2 text-fg-subtle" : "bg-accent/15 text-accent"
        }`}
      >
        <Icon size={16} />
      </span>
      <div className="min-w-0 flex-1">
        <p className="text-sm text-fg">{n.message}</p>
        <div className="mt-1 flex items-center gap-2 text-xs text-fg-subtle">
          {n.actorName && <span className="font-medium text-fg-muted">{n.actorName}</span>}
          <RelativeTime iso={n.createdAt} />
        </div>
      </div>
      {!n.isRead && <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-accent" aria-label="Unread" />}
    </div>
  );

  // Clicking a linked notification marks it read and navigates; unlinked ones just toggle read.
  const handleClick = () => { if (!n.isRead) onMarkRead(); };

  return (
    <div className="group/row relative">
      {n.link ? (
        <Link href={n.link} onClick={handleClick} className="block">{body}</Link>
      ) : (
        <button onClick={handleClick} className="block w-full text-left">{body}</button>
      )}
      {threadId && (
        // Sits above the row's own link, so muting never navigates you into the thread you're
        // trying to escape. Always visible on touch, where there is no hover.
        <button
          type="button"
          onClick={() => mute.mutate(threadId)}
          disabled={mute.isPending}
          title="Stop notifications from this thread"
          className="absolute bottom-2 right-2 inline-flex items-center gap-1.5 rounded-md border border-border bg-surface px-2 py-1 text-[11px] font-medium text-fg-subtle transition-colors hover:border-accent/50 hover:text-fg disabled:opacity-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent sm:opacity-0 sm:group-hover/row:opacity-100 sm:focus-visible:opacity-100"
        >
          <BellOff size={12} aria-hidden /> Mute thread
        </button>
      )}
    </div>
  );
}
