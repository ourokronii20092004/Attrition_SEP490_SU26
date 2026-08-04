"use client";

import { Suspense, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Heart, MapPin, Clock, Gamepad2, ChevronRight, Skull } from "lucide-react";
import { charactersApi } from "@/lib/api/characters";
import { useAuth } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Card } from "@/components/ui/card";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { RelativeTime } from "@/components/ui/relative-time";
import { Pagination } from "@/components/ui/pagination";
import { qk } from "@/lib/query-keys";
import type { CharacterSummaryDto } from "@/lib/types";
import { useLoginHref } from "@/lib/hooks/use-login-href";
import { useUrlPagination } from "@/lib/hooks/use-url-pagination";
import { formatPlaytime } from "@/lib/format-duration";

function CharactersList() {
  const loginHref = useLoginHref();
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (authLoading) return;
    if (!user) router.push(loginHref);
  }, [user, authLoading, router, loginHref]);

  const { data: characters = [], isPending } = useQuery({
    queryKey: qk.characters.mine(),
    enabled: !!user && !authLoading,
    queryFn: async () => {
      const res = await charactersApi.getMine();
      return res.success ? res.data ?? [] : [];
    },
  });

  const { page, setPage, totalPages, paged } = useUrlPagination(characters, 10);

  if (!user && !authLoading) return null;

  return (
    <PageShell size="lg">
      <PageTitle description="Every character you've played, with its save history. Open one to inspect its stats and saves.">
        Your Characters
      </PageTitle>

      {authLoading || isPending ? (
        <SkeletonList rows={4} />
      ) : characters.length === 0 ? (
        <EmptyState
          icon={Gamepad2}
          title="No characters yet"
          description="Play a session in the game client and your characters will appear here."
        />
      ) : (
        <div className="stagger space-y-3">
          {paged.map((c, i) => (
            <div key={c.id} style={{ "--i": i } as React.CSSProperties}>
              <CharacterRow character={c} />
            </div>
          ))}
          <Pagination page={page} totalPages={totalPages} onChange={setPage} />
        </div>
      )}
    </PageShell>
  );
}

/**
 * A character in the list. Was an accordion that lazily loaded inventory inline; now a link, because
 * the detail page has to exist anyway to host the save-file rail, and two places rendering the same
 * stats would drift apart.
 */
function CharacterRow({ character }: { character: CharacterSummaryDto }) {
  const snap = character.latestSnapshot;

  return (
    <Link
      href={`/characters/${encodeURIComponent(character.id)}`}
      className="group block rounded-card focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
    >
      <Card className="flex items-center gap-4 p-4 transition-colors group-hover:bg-surface-2">
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-accent-soft font-display text-lg font-bold text-accent">
          {character.name[0]?.toUpperCase() ?? "?"}
        </div>

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="truncate font-medium text-fg group-hover:text-accent">{character.name}</h3>
            <span className="rounded-full bg-surface-3 px-2 py-0.5 text-xs text-fg-muted">{character.archetype}</span>
            {snap && (
              <span
                className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ${
                  snap.isAlive ? "bg-success/10 text-success" : "bg-danger/10 text-danger"
                }`}
              >
                {snap.isAlive ? "Alive" : <><Skull size={11} aria-hidden /> Dead</>}
              </span>
            )}
          </div>

          {snap ? (
            <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-fg-muted">
              <span>Lv.{snap.level}</span>
              <span className="flex items-center gap-1"><Heart size={12} aria-hidden /> {snap.hp}/{snap.maxHp}</span>
              {snap.roomCode && (
                <span className="flex items-center gap-1"><MapPin size={12} aria-hidden /> {snap.roomCode}</span>
              )}
              {snap.playtimeSeconds > 0 && <span>{formatPlaytime(snap.playtimeSeconds)}</span>}
              <span className="flex items-center gap-1">
                <Clock size={12} aria-hidden /> <RelativeTime iso={snap.capturedAt} />
              </span>
            </div>
          ) : (
            <p className="mt-1 text-xs text-fg-subtle">No status reported yet</p>
          )}
        </div>

        <div className="shrink-0 text-right">
          <p className="text-xs text-fg-subtle">
            {character.snapshotCount} {character.snapshotCount === 1 ? "save" : "saves"}
          </p>
        </div>
        <ChevronRight size={18} className="shrink-0 text-fg-subtle transition-colors group-hover:text-accent" aria-hidden />
      </Card>
    </Link>
  );
}

export default function CharactersPage() {
  return (
    <Suspense fallback={null}>
      <CharactersList />
    </Suspense>
  );
}
