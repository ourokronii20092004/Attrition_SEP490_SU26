"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useAuth } from "@/lib/providers";
import { adminApi } from "@/lib/api/admin";
import { PageLoader } from "@/components/ui/spinner";
import type { AdminStatsDto } from "@/lib/types";

export default function AdminPage() {
  const { user, loading: authLoading } = useAuth();
  const [stats, setStats] = useState<AdminStatsDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!user || user.role !== "Admin") return;
    adminApi
      .getStats()
      .then((res) => {
        if (res.success) setStats(res.data);
      })
      .finally(() => setLoading(false));
  }, [user]);

  if (authLoading) return <PageLoader />;
  if (!user || user.role !== "Admin") {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12 text-center">
        <h1 className="font-display text-3xl font-bold text-fg">Access Denied</h1>
        <p className="mt-4 text-fg-muted">You need admin privileges to view this page.</p>
      </div>
    );
  }

  if (loading) return <PageLoader />;

  const cards = [
    { label: "Users", value: stats?.totalUsers, href: "/admin/users" },
    { label: "Wiki Articles", value: stats?.totalWikiArticles, href: "/admin/wiki" },
    { label: "Pending Contributions", value: stats?.pendingContributions, href: "/admin/wiki" },
    { label: "Forum Threads", value: stats?.totalForumThreads, href: "/admin/forum" },
    { label: "Forum Posts", value: stats?.totalForumPosts, href: "/admin/forum" },
    { label: "Removed Posts", value: stats?.removedPosts, href: "/admin/forum" },
    { label: "Enemies", value: stats?.totalEnemies, href: "/admin/enemies" },
    { label: "Assets", value: stats?.totalAssets, href: "/admin/assets" },
    { label: "Music Albums", value: stats?.totalMusicAlbums, href: "/admin/music" },
    { label: "Music Tracks", value: stats?.totalMusicTracks, href: "/admin/music" },
  ];

  return (
    <div className="mx-auto max-w-6xl px-4 py-8">
      <h1 className="font-display text-4xl font-bold text-fg">Admin Dashboard</h1>
      <p className="mt-2 text-fg-muted">Overview of the Attrition platform</p>

      {stats?.unavailableSources && stats.unavailableSources.length > 0 && (
        <div className="mt-4 rounded-lg bg-warning/10 px-4 py-3 text-sm text-warning">
          Unavailable services: {stats.unavailableSources.join(", ")}
        </div>
      )}

      <div className="mt-6 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {cards.map((card) => (
          <Link key={card.label} href={card.href} className="card p-4 transition hover:border-accent">
            <p className="text-sm text-fg-muted">{card.label}</p>
            <p className="mt-1 text-3xl font-bold text-fg">{card.value ?? "—"}</p>
          </Link>
        ))}
      </div>

      <nav className="mt-10">
        <h2 className="text-lg font-semibold text-fg">Management</h2>
        <div className="mt-4 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <AdminLink href="/admin/users" label="Users" desc="Manage roles, bans, passwords" />
          <AdminLink href="/admin/wiki" label="Wiki" desc="Articles, categories, contributions" />
          <AdminLink href="/admin/forum" label="Forum" desc="Moderation, reports, categories" />
          <AdminLink href="/admin/enemies" label="Enemies" desc="Create, edit, delete enemies" />
          <AdminLink href="/admin/assets" label="Assets" desc="Upload and manage assets" />
          <AdminLink href="/admin/music" label="Music" desc="Albums, tracks, uploads" />
          <AdminLink href="/admin/characters" label="Characters" desc="View all players' character status" />
        </div>
      </nav>
    </div>
  );
}

function AdminLink({ href, label, desc }: { href: string; label: string; desc: string }) {
  return (
    <Link href={href} className="card p-4 transition hover:border-accent">
      <p className="font-medium text-fg">{label}</p>
      <p className="mt-1 text-sm text-fg-muted">{desc}</p>
    </Link>
  );
}
