"use client";

import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { MessagesSquare, BookOpen, CalendarDays, Shield, Clock, UserX } from "lucide-react";
import { useAuth } from "@/lib/providers";
import { accountApi } from "@/lib/api/account";
import { forumApi } from "@/lib/api/forum";
import { wikiApi } from "@/lib/api/wiki";
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

  // The Identity service stores postCount/contributionCount as denormalized columns, but nothing
  // maintains them (forum posts & wiki articles live in other services), so they're always 0.
  // Derive the real counts from the public author-filtered endpoints instead — same source as the
  // activity feed below, so the stat and the list always agree.
  const userId = profile?.id;
  const { data: counts, isLoading: countsLoading } = useQuery({
    queryKey: qk.profileCounts(userId ?? ""),
    enabled: !!userId,
    queryFn: async () => {
      const [threads, contributions] = await Promise.all([
        forumApi.getThreads({ authorId: userId!, page: 1, pageSize: 1 }),
        // Authored articles + approved suggested edits (regular users contribute via edits, which
        // count once approved). Same live source as the wiki, so the stat reflects approvals.
        wikiApi.getUserContributionCount(userId!),
      ]);
      return {
        posts: threads.success ? threads.data.totalCount : 0,
        contributions: contributions.success ? contributions.data : 0,
      };
    },
  });

  const isOwner = !!user && !!profile && user.username === profile.username;
  const onEdited = async () => { await refreshUser(); refetch(); };

  if (isPending) return <ProfileSkeleton />;

  if (!profile) {
    return (
      <PageShell size="lg">
        <div className="mb-4"><BackButton label="Back" fallbackHref="/" /></div>
        <EmptyState icon={UserX} title="User not found" description="This profile doesn't exist or has been removed." />
      </PageShell>
    );
  }

  // Owner sees their own live auth record (so edits reflect instantly); everyone else sees the
  // PII-free public profile. Only non-sensitive fields are rendered below either way.
  const display = isOwner && user ? user : profile;
  // Stats come from the public profile record (getProfile), which resolves live counts server-side.
  // The owner's live `user` object carries stale stored counters, so always read stats from `profile`.
  const t = tenure(profile.joinedAt);

  return (
    <PageShell size="lg">
      <div className="mb-5"><BackButton label="Back" fallbackHref="/" /></div>

      {/* Cover banner — inset to content width with all corners rounded, so it reads as its own panel. */}
      <ProfileBanner profile={display} isOwner={isOwner} onEdited={onEdited} />

      {/* Identity dossier — rests just over the banner's lower edge (light overlap keeps the cover visible);
          the avatar straddles the seam. */}
      <section className="relative z-10 -mt-4 rounded-card border border-border bg-surface/80 p-6 shadow-[var(--shadow-glow)] backdrop-blur sm:-mt-4 sm:p-8">
        <div className="flex flex-col items-center gap-5 sm:flex-row sm:items-end sm:gap-6">
          <div className="-mt-20 sm:-mt-24">
            <ProfileAvatar profile={display} isOwner={isOwner} onEdited={onEdited} />
          </div>

          <div className="min-w-0 flex-1 text-center sm:text-left">
            <ProfileName profile={display} isOwner={isOwner} onEdited={onEdited} />
            <p className="mt-1 font-mono text-sm text-fg-muted">@{display.username}</p>

            <div className="mt-3 flex flex-wrap items-center justify-center gap-2 sm:justify-start">
              <RoleBadge role={display.role} />
              <MetaChip icon={CalendarDays}>Joined {formatDate(display.joinedAt)}</MetaChip>
              {display.authProvider && <MetaChip icon={Shield}>{display.authProvider}</MetaChip>}
            </div>
          </div>

          {!isOwner && user && (
            <div className="flex w-full justify-center sm:w-auto sm:justify-end">
              <ReportUserButton userId={display.id} username={display.username} />
            </div>
          )}
        </div>

        <div className="mt-6">
          <ProfileBio profile={display} isOwner={isOwner} onEdited={onEdited} />
        </div>
      </section>

      {/* Record stats — forum/wiki counts are fetched live (see the counts query above). */}
      <div className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-3">
        <StatCard icon={MessagesSquare} value={counts?.posts ?? 0} loading={countsLoading} label="Forum threads" hint="discussions started" />
        <StatCard icon={BookOpen} value={counts?.contributions ?? 0} loading={countsLoading} label="Wiki contributions" hint="articles & approved edits" />
        <StatCard icon={Clock} value={t.value} unit={t.unit} label="In the archive" hint={`since ${formatDate(display.joinedAt)}`} />
      </div>

      <ProfileActivity userId={display.id} username={display.username} />
    </PageShell>
  );
}

function RoleBadge({ role }: { role: string }) {
  const admin = role === "Admin";
  return (
    <span
      className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium ${
        admin ? "bg-accent-soft text-accent ring-1 ring-accent/30" : "bg-surface-2 text-fg-muted"
      }`}
    >
      <Shield size={12} /> {role}
    </span>
  );
}

function MetaChip({ icon: Icon, children }: { icon: React.ComponentType<{ size?: number }>; children: React.ReactNode }) {
  return (
    <span className="inline-flex items-center gap-1.5 rounded-full bg-surface-2 px-2.5 py-1 text-xs text-fg-muted">
      <Icon size={12} /> {children}
    </span>
  );
}

function StatCard({ icon: Icon, value, unit, label, hint, loading }: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  value: number; unit?: string; label: string; hint?: string; loading?: boolean;
}) {
  return (
    <div className="group relative overflow-hidden rounded-card border border-border bg-surface p-5 transition-[transform,border-color,box-shadow] duration-300 ease-[cubic-bezier(0.16,1,0.3,1)] hover:-translate-y-1 hover:border-accent/50 hover:shadow-[var(--shadow-glow)]">
      <span aria-hidden className="pointer-events-none absolute -right-8 -top-8 h-24 w-24 rounded-full bg-accent/10 opacity-0 blur-2xl transition-opacity duration-500 group-hover:opacity-100" />
      <span className="inline-flex h-10 w-10 items-center justify-center rounded-md border border-border bg-surface-2 text-fg-subtle transition-colors duration-300 group-hover:border-accent/40 group-hover:text-accent">
        <Icon size={18} />
      </span>
      <p className="mt-4 font-display text-3xl font-bold leading-none tabular-nums text-fg">
        {loading ? (
          <span className="skeleton inline-block h-7 w-14 rounded align-middle" aria-hidden />
        ) : (
          <>
            {value.toLocaleString()}
            {unit && <span className="ml-1.5 text-lg font-semibold text-fg-muted">{unit}</span>}
          </>
        )}
      </p>
      <p className="mt-2 text-sm font-medium text-fg">{label}</p>
      {hint && <p className="mt-0.5 text-xs text-fg-subtle">{hint}</p>}
    </div>
  );
}

/** Membership tenure from joinedAt, collapsed to the largest sensible unit. Client-only (post-mount). */
function tenure(joinedAt: string): { value: number; unit: string } {
  const days = Math.max(0, Math.floor((Date.now() - new Date(joinedAt).getTime()) / 86_400_000));
  if (days < 60) return { value: days, unit: days === 1 ? "day" : "days" };
  const months = Math.floor(days / 30);
  if (months < 24) return { value: months, unit: months === 1 ? "month" : "months" };
  return { value: Math.floor(days / 365), unit: Math.floor(days / 365) === 1 ? "year" : "years" };
}

function ProfileSkeleton() {
  return (
    <PageShell size="lg">
      <div className="mb-5"><Skeleton className="h-5 w-16" /></div>
      <Skeleton className="h-56 w-full rounded-2xl sm:h-72" />
      <div className="relative z-10 -mt-4 rounded-card border border-border bg-surface/80 p-6 backdrop-blur sm:-mt-4 sm:p-8">
        <div className="flex flex-col items-center gap-5 sm:flex-row sm:items-end sm:gap-6">
          <Skeleton className="-mt-20 h-28 w-28 rounded-full sm:-mt-24 sm:h-32 sm:w-32" />
          <div className="flex-1 space-y-3">
            <Skeleton className="mx-auto h-9 w-56 sm:mx-0" />
            <Skeleton className="mx-auto h-4 w-32 sm:mx-0" />
            <Skeleton className="mx-auto h-6 w-64 sm:mx-0" />
          </div>
        </div>
        <Skeleton className="mt-6 h-24 w-full" />
      </div>
      <div className="mt-5 grid grid-cols-1 gap-4 sm:grid-cols-3">
        {[0, 1, 2].map((i) => <Skeleton key={i} className="h-32 w-full rounded-card" />)}
      </div>
      <div className="mt-12 space-y-3">
        <Skeleton className="h-9 w-48" />
        {[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-[4.25rem] w-full rounded-card" />)}
      </div>
    </PageShell>
  );
}
