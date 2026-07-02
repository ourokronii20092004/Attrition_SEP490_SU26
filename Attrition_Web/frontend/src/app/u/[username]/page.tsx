"use client";

import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { MessagesSquare, BookOpen, CalendarDays, Shield } from "lucide-react";
import { useAuth } from "@/lib/providers";
import { accountApi } from "@/lib/api/account";
import { PageShell } from "@/components/ui/page-shell";
import { Skeleton } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { BackButton } from "@/components/ui/back-button";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import { ProfileActivity } from "./profile-activity";
import { ProfileBanner, ProfileAvatar, ProfileName, ProfileBio } from "./profile-edit";
import { ReportUserButton } from "./report-user-button";

export default function ProfilePage() {
  const params = useParams<{ username: string }>();
  const { user, refreshUser } = useAuth();

  const { data: profile, isPending, refetch } = useQuery({
    queryKey: qk.profile(params.username),
    enabled: !!params.username,
    queryFn: async () => {
      const res = await accountApi.getProfile(params.username);
      return res.success ? res.data : null;
    },
  });

  const isOwner = !!user && !!profile && user.username === profile.username;
  const onEdited = async () => { await refreshUser(); refetch(); };

  if (isPending) {
    return (
      <PageShell size="md">
        <div className="pt-8">
          <div className="-mx-4 h-56 sm:-mx-8 sm:h-64"><Skeleton className="h-full w-full sm:rounded-b-2xl" /></div>
        </div>
        <div className="relative z-10 -mt-12 rounded-card border border-border bg-surface/80 p-6 backdrop-blur">
          <div className="flex flex-col items-center gap-3">
            <Skeleton className="h-24 w-24 rounded-full" />
            <Skeleton className="h-8 w-48" /><Skeleton className="h-4 w-28" />
          </div>
        </div>
      </PageShell>
    );
  }

  if (!profile) {
    return (
      <PageShell size="lg">
        <div className="mb-4"><BackButton label="Back" fallbackHref="/" /></div>
        <EmptyState title="User not found" description="This profile doesn't exist or has been removed." />
      </PageShell>
    );
  }

  // Owner sees their own live auth record (so edits reflect instantly); everyone else sees the
  // PII-free public profile. Only non-sensitive fields are rendered below either way.
  const display = isOwner && user ? user : profile;

  return (
    <PageShell size="md">
      <div className="mb-4"><BackButton label="Back" fallbackHref="/" /></div>

      {/* pt-8 cancels the banner's own -mt-8 bleed so it sits below the back button instead of over it. */}
      <div className="pt-8">
        <ProfileBanner profile={display} isOwner={isOwner} onEdited={onEdited} />
      </div>

      {/* Hero identity card — centered, pokes up over the banner via the avatar only. */}
      <div className="relative z-10 -mt-12 rounded-card border border-border bg-surface/80 px-6 pb-6 pt-0 text-center shadow-[var(--shadow-glow)] backdrop-blur">
        <div className="flex flex-col items-center">
          <ProfileAvatar profile={display} isOwner={isOwner} onEdited={onEdited} />
          <div className="mt-3 flex flex-col items-center gap-1">
            <ProfileName profile={display} isOwner={isOwner} onEdited={onEdited} />
            <p className="text-fg-muted">@{display.username}</p>
          </div>

          {/* Meta chips */}
          <div className="mt-4 flex flex-wrap items-center justify-center gap-2 text-xs">
            <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 font-medium ${display.role === "Admin" ? "bg-accent-soft text-accent" : "bg-surface-2 text-fg-muted"}`}>
              <Shield size={12} /> {display.role}
            </span>
            <span className="inline-flex items-center gap-1.5 rounded-full bg-surface-2 px-2.5 py-1 text-fg-muted">
              <CalendarDays size={12} /> Joined {formatDate(display.joinedAt)}
            </span>
          </div>

          <div className="w-full max-w-xl">
            <ProfileBio profile={display} isOwner={isOwner} onEdited={onEdited} />
          </div>

          {/* Stats strip — centered, divided, always visible in the hero. */}
          <div className="mt-6 flex items-stretch divide-x divide-border rounded-card border border-border bg-surface-2/50">
            <Stat icon={MessagesSquare} value={display.postCount} label="Forum posts" />
            <Stat icon={BookOpen} value={display.contributionCount} label="Wiki contributions" />
          </div>

          {!isOwner && user && (
            <div className="mt-4">
              <ReportUserButton userId={display.id} username={display.username} />
            </div>
          )}
        </div>
      </div>

      {/* Activity feed — full width below the hero. */}
      <div className="mt-8">
        <ProfileActivity userId={display.id} username={display.username} />
      </div>
    </PageShell>
  );
}

function Stat({ icon: Icon, value, label }: { icon: React.ComponentType<{ size?: number; className?: string }>; value: number; label: string }) {
  return (
    <div className="flex min-w-[9rem] items-center justify-center gap-3 px-6 py-4">
      <span className="inline-flex h-10 w-10 shrink-0 items-center justify-center rounded-md border border-border bg-surface text-accent">
        <Icon size={18} />
      </span>
      <div className="text-left">
        <p className="font-display text-2xl font-bold leading-none tabular-nums text-fg">{value}</p>
        <p className="mt-1 text-xs text-fg-muted">{label}</p>
      </div>
    </div>
  );
}
