"use client";

import { useCallback, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { useAuth } from "@/lib/providers";
import { accountApi } from "@/lib/api/account";
import { PageShell } from "@/components/ui/page-shell";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { formatDate } from "@/lib/format-date";
import type { UserDto } from "@/lib/types";
import { ProfileActivity } from "./profile-activity";
import { ProfileBanner, ProfileAvatar, ProfileName } from "./profile-edit";

export default function ProfilePage() {
  const params = useParams<{ username: string }>();
  const { user, refreshUser } = useAuth();
  const [profile, setProfile] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(() => {
    if (!params.username) return;
    setLoading(true);
    accountApi
      .getProfile(params.username)
      .then((res) => { if (res.success) setProfile(res.data); })
      .finally(() => setLoading(false));
  }, [params.username]);

  useEffect(() => { load(); }, [load]);

  const isOwner = !!user && !!profile && user.username === profile.username;
  // Owner edits hit account endpoints, then we refresh both the auth user and this view.
  const onEdited = async () => { await refreshUser(); load(); };

  if (loading) {
    return (
      <PageShell size="md">
        <div className="flex items-center gap-4">
          <Skeleton className="h-24 w-24 rounded-full" />
          <div className="space-y-2"><Skeleton className="h-7 w-40" /><Skeleton className="h-4 w-24" /></div>
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

  const display = isOwner && user ? user : profile;

  return (
    <PageShell size="md">
      <ProfileBanner profile={display} isOwner={isOwner} onEdited={onEdited} />

      <div className="relative flex items-end gap-4">
        <ProfileAvatar profile={display} isOwner={isOwner} onEdited={onEdited} />
        <div className="min-w-0 pb-1">
          <ProfileName profile={display} isOwner={isOwner} onEdited={onEdited} />
          <p className="text-fg-muted">@{display.username}</p>
          <div className="mt-1.5 flex flex-wrap items-center gap-2 text-sm text-fg-subtle">
            <span className="rounded-full bg-surface-2 px-2 py-0.5 text-xs font-medium">{display.role}</span>
            <span>Joined {formatDate(display.joinedAt)}</span>
          </div>
        </div>
      </div>

      {display.bio && <p className="mt-6 leading-relaxed text-fg-muted">{display.bio}</p>}

      <div className="mt-6 grid grid-cols-2 gap-4">
        <Card className="p-5 text-center">
          <p className="font-display text-3xl font-bold text-fg">{display.postCount}</p>
          <p className="mt-1 text-sm text-fg-muted">Posts</p>
        </Card>
        <Card className="p-5 text-center">
          <p className="font-display text-3xl font-bold text-fg">{display.contributionCount}</p>
          <p className="mt-1 text-sm text-fg-muted">Contributions</p>
        </Card>
      </div>

      <ProfileActivity userId={display.id} username={display.username} />
    </PageShell>
  );
}
