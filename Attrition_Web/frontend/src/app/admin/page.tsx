"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import {
  ArrowRight, Users, BookOpen, FileClock, MessagesSquare, MessageSquare, EyeOff,
  Skull, Image as ImageIcon, Disc3, Music, Flag, Gem, Sparkles, Gamepad2, Keyboard,
  DoorOpen, Users2, ShieldAlert, Activity,
} from "lucide-react";
import { adminApi } from "@/lib/api/admin";
import { userReportsApi } from "@/lib/api/user-reports";
import { charactersApi } from "@/lib/api/characters";
import { musicApi } from "@/lib/api/music";
import { Card } from "@/components/ui/card";
import { PageTitle } from "@/components/ui/page-title";
import { Skeleton } from "@/components/ui/skeleton";
import { RelativeTime } from "@/components/ui/relative-time";
import { qk } from "@/lib/query-keys";
import { LIVE_NORMAL, liveWhenFocused } from "@/lib/live";
import { getLastAdminPage } from "./admin-top-bar";
import { adminLabelFor } from "./admin-routes";
import { resolveMediaUrl } from "@/lib/api/media";

type StatCard = {
  label: string; value: number | null | undefined; href: string;
  icon: React.ComponentType<{ size?: number; className?: string }>;
  highlight?: boolean;
};

export default function AdminPage() {
  const [resume, setResume] = useState<string | null>(null);

  const { data: stats, isPending: loading } = useQuery({
    queryKey: qk.admin.stats(),
    // Dashboard counters move as users post and reports arrive.
    refetchInterval: liveWhenFocused(LIVE_NORMAL),
    queryFn: async () => {
      const res = await adminApi.getStats();
      return res.success ? res.data : null;
    },
  });

  // Live moderation + activity feeds for the dashboard.
  const { data: pendingReports = [] } = useQuery({
    queryKey: qk.admin.forum.reports("pending"),
    enabled: !!(stats?.totalUsers != null),
    queryFn: async () => {
      const res = await userReportsApi.adminList({ status: "Pending", page: 1, pageSize: 8 });
      return res.success ? res.data.items : [];
    },
  });
  const { data: recentRooms = [] } = useQuery({
    queryKey: ["admin", "recent", "rooms"],
    queryFn: async () => {
      const res = await charactersApi.getAdminRooms({ page: 1, pageSize: 5 });
      return res.success ? res.data.items : [];
    },
  });
  const { data: recentTracks = [] } = useQuery({
    queryKey: ["admin", "recent", "tracks"],
    queryFn: async () => {
      const res = await musicApi.getTracks();
      if (!res.success) return [];
      return [...res.data].sort((a, b) => b.playCount - a.playCount).slice(0, 5);
    },
  });

  useEffect(() => {
    const last = getLastAdminPage();
    if (last && last !== "/admin") setResume(last);
  }, []);

  const groups: { title: string; cards: StatCard[] }[] = [
    {
      title: "Community",
      cards: [
        { label: "Users", value: stats?.totalUsers, href: "/admin/users", icon: Users },
        { label: "Wiki Articles", value: stats?.totalWikiArticles, href: "/admin/wiki/articles", icon: BookOpen },
        { label: "Pending Contributions", value: stats?.pendingContributions, href: "/admin/wiki/queue", icon: FileClock, highlight: (stats?.pendingContributions ?? 0) > 0 },
        { label: "Forum Threads", value: stats?.totalForumThreads, href: "/admin/forum/threads", icon: MessagesSquare },
        { label: "Forum Posts", value: stats?.totalForumPosts, href: "/admin/forum/threads", icon: MessageSquare },
        { label: "Removed Posts", value: stats?.removedPosts, href: "/admin/forum/reports", icon: EyeOff },
      ],
    },
    {
      title: "Game Content",
      cards: [
        { label: "Enemies", value: stats?.totalEnemies, href: "/admin/enemies", icon: Skull },
        { label: "Items", value: stats?.totalItems, href: "/admin/items", icon: Gem },
        { label: "Skills", value: stats?.totalSkills, href: "/admin/skills", icon: Sparkles },
        { label: "Assets", value: stats?.totalAssets, href: "/admin/assets", icon: ImageIcon },
        { label: "Music Albums", value: stats?.totalMusicAlbums, href: "/admin/music/albums", icon: Disc3 },
        { label: "Music Tracks", value: stats?.totalMusicTracks, href: "/admin/music", icon: Music },
      ],
    },
    {
      // What players have actually done, as opposed to what has been authored for them.
      title: "Player Activity",
      cards: [
        { label: "Characters", value: stats?.totalCharacters, href: "/admin/characters", icon: Gamepad2 },
        { label: "Rooms", value: stats?.totalRooms, href: "/admin/rooms", icon: DoorOpen },
        { label: "Co-op Rooms", value: stats?.totalCoopRooms, href: "/admin/rooms", icon: Users2 },
      ],
    },
  ];

  const quickActions = [
    { label: "Review reports", href: "/admin/user-reports", icon: Flag },
    { label: "Add enemy", href: "/admin/enemies", icon: Skull },
    { label: "Add item", href: "/admin/items", icon: Gem },
    { label: "Manage skills", href: "/admin/skills", icon: Sparkles },
    { label: "Upload asset", href: "/admin/assets", icon: ImageIcon },
    { label: "Characters", href: "/admin/characters", icon: Gamepad2 },
    { label: "Co-op rooms", href: "/admin/rooms", icon: DoorOpen },
  ];

  return (
    <div className="mx-auto max-w-6xl">
      <PageTitle description="Overview of the Attrition platform.">Dashboard</PageTitle>

      {resume && (
        <Link
          href={resume}
          className="mb-4 flex items-center justify-between gap-3 rounded-lg border border-accent/30 bg-accent-soft px-4 py-3 text-sm transition-colors hover:border-accent/60"
        >
          <span className="text-fg">Resume where you left off — <span className="font-medium text-accent">{adminLabelFor(resume)}</span></span>
          <ArrowRight size={16} className="shrink-0 text-accent" />
        </Link>
      )}

      {stats?.unavailableSources && stats.unavailableSources.length > 0 && (
        <div className="mb-4 rounded-lg border border-warning/30 bg-warning/10 px-4 py-3 text-sm text-warning">
          Unavailable services: {stats.unavailableSources.join(", ")}
        </div>
      )}

      {/* Quick actions */}
      <div className="mb-6 flex flex-wrap gap-2">
        {quickActions.map((a) => (
          <Link
            key={a.href}
            href={a.href}
            className="inline-flex items-center gap-2 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-fg-muted transition-colors hover:border-accent/50 hover:text-fg"
          >
            <a.icon size={15} className="text-accent" /> {a.label}
          </Link>
        ))}
      </div>

      {/* ── Live attention + activity: what actually needs an admin right now ── */}
      <div className="mb-6 grid gap-4 lg:grid-cols-2">
        <Card className="p-4">
          <h2 className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
            <ShieldAlert size={13} /> Needs attention
          </h2>
          {pendingReports.length === 0 ? (
            <p className="mt-3 text-sm text-fg-muted">No pending user reports. Clear.</p>
          ) : (
            <ul className="mt-2 divide-y divide-border/60">
              {pendingReports.map((r) => (
                <li key={r.id}>
                  <Link href={`/admin/user-reports`} className="flex items-center justify-between gap-3 py-2 text-sm transition-colors hover:text-accent">
                    <span className="truncate text-fg">{r.reportedUserName}</span>
                    <span className="shrink-0 text-xs text-fg-subtle">{r.reason}</span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Card>

        <Card className="p-4">
          <h2 className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
            <Activity size={13} /> Recent co-op rooms
          </h2>
          {recentRooms.length === 0 ? (
            <p className="mt-3 text-sm text-fg-muted">No rooms yet.</p>
          ) : (
            <ul className="mt-2 divide-y divide-border/60">
              {recentRooms.map((r) => (
                <li key={r.id}>
                  <Link href={`/admin/rooms/${r.id}`} className="flex items-center justify-between gap-3 py-2 text-sm transition-colors hover:text-accent">
                    <span className="truncate font-medium text-fg">{r.roomCode} <span className="font-normal text-fg-muted">{r.name}</span></span>
                    <span className="shrink-0 text-xs text-fg-subtle"><RelativeTime iso={r.lastPlayedAt} /></span>
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Card>
      </div>

      {/* ── Most-played tracks ── */}
      {recentTracks.length > 0 && (
        <Card className="mb-6 p-4">
          <h2 className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
            <Music size={13} /> Most played
          </h2>
          <ul className="mt-2 divide-y divide-border/60">
            {recentTracks.map((t, i) => (
              <li key={t.trackId}>
                <Link href={`/admin/music/albums/${t.albumId}`} className="flex items-center gap-3 py-2 text-sm transition-colors hover:text-accent">
                  <span className="w-4 shrink-0 text-right text-xs tabular-nums text-fg-subtle">{i + 1}</span>
                  {t.coverPath || t.albumCoverPath ? (
                    <img src={resolveMediaUrl(t.coverPath ?? t.albumCoverPath) ?? ""} alt="" className="h-8 w-8 shrink-0 rounded object-cover" />
                  ) : (
                    <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded bg-surface-2 text-fg-subtle"><Disc3 size={14} /></span>
                  )}
                  <span className="min-w-0 flex-1 truncate font-medium text-fg">{t.title}</span>
                  <span className="shrink-0 text-xs tabular-nums text-fg-subtle">{t.playCount} plays</span>
                </Link>
              </li>
            ))}
          </ul>
        </Card>
      )}

      {groups.map((group) => (
        <div key={group.title} className="mb-6">
          <h2 className="mb-3 text-xs font-semibold uppercase tracking-wider text-fg-subtle">{group.title}</h2>
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
            {group.cards.map((card) => (
              <Card key={card.label} interactive className="p-0">
                <Link href={card.href} className="flex items-start gap-3 p-4">
                  <span className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-lg ${card.highlight ? "bg-warning/15 text-warning" : "bg-accent-soft text-accent"}`}>
                    <card.icon size={18} />
                  </span>
                  <div className="min-w-0">
                    <p className="text-xs text-fg-muted">{card.label}</p>
                    {loading ? (
                      <Skeleton className="mt-1.5 h-7 w-12" />
                    ) : (
                      <p className="mt-0.5 font-display text-2xl font-bold tabular-nums text-fg">{card.value ?? "—"}</p>
                    )}
                  </div>
                </Link>
              </Card>
            ))}
          </div>
        </div>
      ))}

      <p className="mt-2 flex items-center gap-1.5 text-xs text-fg-subtle">
        <Keyboard size={13} /> Tip: press <kbd className="rounded border border-border bg-surface-2 px-1">?</kbd> for keyboard shortcuts, <kbd className="rounded border border-border bg-surface-2 px-1">⌘K</kbd> to search.
      </p>
    </div>
  );
}
