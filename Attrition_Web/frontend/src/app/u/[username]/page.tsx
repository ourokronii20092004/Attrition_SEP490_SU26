"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { accountApi } from "@/lib/api/account";
import { resolveMediaUrl } from "@/lib/api/media";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Avatar } from "@/components/ui/avatar";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { formatDate } from "@/lib/format-date";
import type { UserDto } from "@/lib/types";

export default function ProfilePage() {
  const params = useParams<{ username: string }>();
  const [profile, setProfile] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!params.username) return;
    let ignore = false;
    setLoading(true);
    accountApi
      .getProfile(params.username)
      .then((res) => {
        if (!ignore && res.success) setProfile(res.data);
      })
      .finally(() => { if (!ignore) setLoading(false); });
    return () => { ignore = true; };
  }, [params.username]);

  if (loading) {
    return (
      <PageShell size="md">
        <div className="flex items-center gap-4">
          <Skeleton className="h-20 w-20 rounded-full" />
          <div className="space-y-2">
            <Skeleton className="h-7 w-40" />
            <Skeleton className="h-4 w-24" />
          </div>
        </div>
      </PageShell>
    );
  }

  if (!profile) {
    return (
      <PageShell size="md">
        <EmptyState title="User not found" description="This profile doesn't exist or has been removed." />
      </PageShell>
    );
  }

  return (
    <PageShell size="md">
      {profile.backgroundUrl && (
        <div className="relative -mx-4 -mt-8 mb-6 h-48 overflow-hidden sm:rounded-b-2xl">
          <img src={resolveMediaUrl(profile.backgroundUrl) ?? ""} alt="" className="h-full w-full object-cover" />
          <div className="absolute inset-0 bg-gradient-to-t from-bg to-transparent" />
        </div>
      )}

      <div className="flex items-center gap-4">
        <span className="rounded-full ring-4 ring-bg">
          <Avatar src={profile.avatarUrl} name={profile.displayName ?? profile.username} size="xl" />
        </span>
        <div className="min-w-0">
          <h1 className="font-display text-3xl font-bold tracking-tight text-fg">{profile.displayName ?? profile.username}</h1>
          <p className="text-fg-muted">@{profile.username}</p>
          <div className="mt-1.5 flex flex-wrap items-center gap-2 text-sm text-fg-subtle">
            <span className="rounded-full bg-surface-2 px-2 py-0.5 text-xs font-medium">{profile.role}</span>
            <span>Joined {formatDate(profile.joinedAt)}</span>
          </div>
        </div>
      </div>

      {profile.bio && <p className="mt-6 leading-relaxed text-fg-muted">{profile.bio}</p>}

      <div className="mt-6 grid grid-cols-2 gap-4">
        <Card className="p-5 text-center">
          <p className="font-display text-3xl font-bold text-fg">{profile.postCount}</p>
          <p className="mt-1 text-sm text-fg-muted">Posts</p>
        </Card>
        <Card className="p-5 text-center">
          <p className="font-display text-3xl font-bold text-fg">{profile.contributionCount}</p>
          <p className="mt-1 text-sm text-fg-muted">Contributions</p>
        </Card>
      </div>
    </PageShell>
  );
}
