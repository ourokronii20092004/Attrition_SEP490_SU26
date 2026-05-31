"use client";

import { useEffect, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { ArrowRight } from "lucide-react";
import { adminApi } from "@/lib/api/admin";
import { Card } from "@/components/ui/card";
import { PageTitle } from "@/components/ui/page-title";
import { Skeleton } from "@/components/ui/skeleton";
import { getLastAdminPage } from "./admin-top-bar";
import { adminLabelFor } from "./admin-routes";

export default function AdminPage() {
  const [resume, setResume] = useState<string | null>(null);

  const { data: stats, isPending: loading } = useQuery({
    queryKey: ["admin", "stats"],
    queryFn: async () => {
      const res = await adminApi.getStats();
      return res.success ? res.data : null;
    },
  });

  useEffect(() => {
    // Offer to jump back to the last non-dashboard admin page visited.
    const last = getLastAdminPage();
    if (last && last !== "/admin") setResume(last);
  }, []);

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
    <div className="mx-auto max-w-6xl">
      <PageTitle description="Overview of the Attrition platform.">Dashboard</PageTitle>

      {resume && (
        <Link
          href={resume}
          className="mb-6 flex items-center justify-between gap-3 rounded-lg border border-accent/30 bg-accent-soft px-4 py-3 text-sm transition-colors hover:border-accent/60"
        >
          <span className="text-fg">Resume where you left off — <span className="font-medium text-accent">{adminLabelFor(resume)}</span></span>
          <ArrowRight size={16} className="shrink-0 text-accent" />
        </Link>
      )}

      {stats?.unavailableSources && stats.unavailableSources.length > 0 && (
        <div className="mb-6 rounded-lg border border-warning/30 bg-warning/10 px-4 py-3 text-sm text-warning">
          Unavailable services: {stats.unavailableSources.join(", ")}
        </div>
      )}

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
        {cards.map((card) => (
          <Card key={card.label} interactive className="p-0">
            <Link href={card.href} className="block p-5">
              <p className="text-sm text-fg-muted">{card.label}</p>
              {loading ? (
                <Skeleton className="mt-2 h-9 w-16" />
              ) : (
                <p className="mt-1 font-display text-3xl font-bold tabular-nums text-fg">{card.value ?? "—"}</p>
              )}
            </Link>
          </Card>
        ))}
      </div>
    </div>
  );
}
