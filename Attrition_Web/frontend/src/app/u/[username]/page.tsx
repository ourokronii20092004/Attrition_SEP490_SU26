"use client";

import { useParams } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { MessagesSquare, BookOpen, CalendarDays, Shield, Clock, UserX, EyeOff, BarChart3, ScrollText } from "lucide-react";
import { useAuth } from "@/lib/providers";
import { accountApi } from "@/lib/api/account";
import { ApiError } from "@/lib/api/client";
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

  const { data: profile, isPending, error, refetch } = useQuery({
    queryKey: qk.profile(params.username),
    enabled: !!params.username,
    // A hidden profile answers 403; let that surface as an error so the two cases can be told
    // apart below (403 = "they hid it", anything else = "not found"). Retrying won't help either.
    retry: false,
    queryFn: async () => {
      const res = await accountApi.getProfile(params.username);
      return res.success ? res.data : null;
    },
  });
  const isHidden = error instanceof ApiError && error.status === 403;

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

  if (isHidden) {
    return (
      <PageShell size="lg">
        <div className="mb-4"><BackButton label="Back" fallbackHref="/" /></div>
        <EmptyState icon={EyeOff} title="This profile is private"
          description="This user has hidden their profile." />
      </PageShell>
    );
  }

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
  const t = tenure(display.joinedAt);
  // The owner always sees their own feed; for everyone else the server's flag decides.
  // ponytail: presentation-level gate — the feed's own endpoints (forum threads/replies, wiki
  // contributions) live in other services and stay publicly queryable by userId, so this hides
  // the feed from the profile page but does not make the data private. To enforce it properly,
  // those services need to consult the flag (Forum.Service already has an IdentityClient seam)
  // or Identity needs to expose it on the internal user-summary lookup they already call.
  const showActivity = isOwner || profile.showActivity !== false;

  return (
    <PageShell size="lg">
      <div className="mb-5"><BackButton label="Back" fallbackHref="/" /></div>

      {/* ── Hero ──────────────────────────────────────────────────────────────
          The cover banner stays fully visible as its own panel; only the avatar
          straddles its lower-left edge. Identity text lives below the banner on the
          page itself, so nothing large ever covers the background art. */}
      <section className="animate-rise-in">
        <ProfileBanner profile={display} isOwner={isOwner} onEdited={onEdited} />

        <div className="relative z-10 flex flex-col gap-4 sm:flex-row sm:items-end sm:gap-6 sm:px-2">
          <div className="-mt-16 shrink-0 self-center sm:-mt-20 sm:self-auto">
            <ProfileAvatar profile={display} isOwner={isOwner} onEdited={onEdited} />
          </div>

          <div className="min-w-0 flex-1 text-center sm:pb-1 sm:text-left">
            <ProfileName profile={display} isOwner={isOwner} onEdited={onEdited} />
            <p className="mt-1 font-mono text-sm text-fg-muted">@{display.username}</p>

            <div className="mt-3 flex flex-wrap items-center justify-center gap-2 sm:justify-start">
              <RoleBadge role={display.role} />
              <MetaChip icon={CalendarDays}>Joined {formatDate(display.joinedAt)}</MetaChip>
            </div>
          </div>

          {!isOwner && user && (
            <div className="flex justify-center sm:block sm:pb-1">
              <ReportUserButton userId={display.id} username={display.username} />
            </div>
          )}
        </div>
      </section>

      {/* ── Body: sticky dossier sidebar + activity ─────────────────────────── */}
      <div className="mt-8 grid grid-cols-1 gap-6 lg:mt-10 lg:grid-cols-[19rem_1fr] lg:gap-8">
        <aside className="stagger space-y-5 lg:sticky lg:top-24 lg:self-start">
          {(display.bio || isOwner) && (
            <Panel label="Field note" icon={ScrollText}>
              <ProfileBio profile={display} isOwner={isOwner} onEdited={onEdited} />
            </Panel>
          )}

          <Panel label="Record" icon={BarChart3}>
            <ul className="-my-1">
              <StatRow icon={MessagesSquare} label="Forum threads" hint="discussions started" value={counts?.posts ?? 0} loading={countsLoading} />
              <StatRow icon={BookOpen} label="Wiki contributions" hint="articles & approved edits" value={counts?.contributions ?? 0} loading={countsLoading} />
              <StatRow icon={Clock} label="In the archive" value={t.value} unit={t.unit} hint={`since ${formatDate(display.joinedAt)}`} last />
            </ul>
          </Panel>
        </aside>

        <div className="min-w-0">
          {showActivity ? (
            <ProfileActivity userId={display.id} username={display.username} />
          ) : (
            <EmptyState icon={EyeOff} title="Activity hidden"
              description={isOwner
                ? "Your activity is hidden from other people. You can change this in account settings."
                : "This user has chosen not to show their activity."} />
          )}
        </div>
      </div>
    </PageShell>
  );
}

/* ── Building blocks ─────────────────────────────────────────────────────── */

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

/** Labeled dossier panel — mono caption + hairline, translucent surface. */
function Panel({ label, icon: Icon, children }: {
  label: string; icon: React.ComponentType<{ size?: number; className?: string }>; children: React.ReactNode;
}) {
  return (
    <section className="rounded-card border border-border bg-surface/70 p-5 backdrop-blur">
      <div className="mb-4 flex items-center gap-2 border-b border-border pb-3">
        <Icon size={13} className="text-accent" />
        <h2 className="font-mono text-[11px] uppercase tracking-[0.25em] text-fg-subtle">{label}</h2>
      </div>
      {children}
    </section>
  );
}

/** One line of the record sheet: icon chip, label + hint, big tabular value on the right. */
function StatRow({ icon: Icon, label, hint, value, unit, loading, last }: {
  icon: React.ComponentType<{ size?: number; className?: string }>;
  label: string; hint?: string; value: number; unit?: string; loading?: boolean; last?: boolean;
}) {
  return (
    <li className={`group flex items-center gap-3.5 py-3.5 ${last ? "" : "border-b border-border"}`}>
      <span className="inline-flex h-9 w-9 shrink-0 items-center justify-center rounded-md border border-border bg-surface-2 text-fg-subtle transition-colors group-hover:border-accent/40 group-hover:text-accent">
        <Icon size={16} />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block text-sm font-medium text-fg">{label}</span>
        {hint && <span className="block truncate text-xs text-fg-subtle">{hint}</span>}
      </span>
      <span className="font-display text-2xl font-bold leading-none tabular-nums text-fg">
        {loading ? (
          <span className="skeleton inline-block h-6 w-10 rounded align-middle" aria-hidden />
        ) : (
          <>
            {value.toLocaleString()}
            {unit && <span className="ml-1 text-sm font-semibold text-fg-muted">{unit}</span>}
          </>
        )}
      </span>
    </li>
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
      <Skeleton className="h-60 w-full rounded-3xl sm:h-80" />
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:gap-6 sm:px-2">
        <Skeleton className="-mt-16 h-28 w-28 self-center rounded-full sm:-mt-20 sm:h-32 sm:w-32 sm:self-auto" />
        <div className="flex-1 space-y-3 pb-1 text-center sm:text-left">
          <Skeleton className="mx-auto h-9 w-56 sm:mx-0" />
          <Skeleton className="mx-auto h-4 w-32 sm:mx-0" />
          <Skeleton className="mx-auto h-6 w-64 sm:mx-0" />
        </div>
      </div>
      <div className="mt-8 grid grid-cols-1 gap-6 lg:mt-10 lg:grid-cols-[19rem_1fr] lg:gap-8">
        <div className="space-y-5">
          <Skeleton className="h-40 w-full rounded-card" />
          <Skeleton className="h-56 w-full rounded-card" />
        </div>
        <div className="space-y-3">
          <Skeleton className="h-9 w-48" />
          {[0, 1, 2, 3].map((i) => <Skeleton key={i} className="h-[4.25rem] w-full rounded-card" />)}
        </div>
      </div>
    </PageShell>
  );
}
