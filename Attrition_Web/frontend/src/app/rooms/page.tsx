"use client";

import { useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQuery } from "@tanstack/react-query";
import { Users, Map, Clock, DoorOpen, ChevronRight } from "lucide-react";
import { sessionsApi } from "@/lib/api/sessions";
import { useAuth } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { PageTitle } from "@/components/ui/page-title";
import { Card } from "@/components/ui/card";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { RelativeTime } from "@/components/ui/relative-time";
import { Pagination } from "@/components/ui/pagination";
import { useClientPagination } from "@/lib/hooks/use-client-pagination";
import { qk } from "@/lib/query-keys";
import { useLoginHref } from "@/lib/hooks/use-login-href";
import { formatPlaytime } from "@/lib/format-duration";
import type { SessionSummaryDto } from "@/lib/types";

export default function RoomsPage() {
  const loginHref = useLoginHref();
  const { user, loading: authLoading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (authLoading) return;
    if (!user) router.push(loginHref);
  }, [user, authLoading, router, loginHref]);

  const { data: rooms = [], isPending } = useQuery({
    queryKey: qk.sessions.mine(),
    enabled: !!user && !authLoading,
    queryFn: async () => {
      const res = await sessionsApi.getMine();
      return res.success ? res.data ?? [] : [];
    },
  });

  const { page, setPage, totalPages, paged } = useClientPagination(rooms, 10);

  if (!user && !authLoading) return null;

  return (
    <PageShell size="lg">
      <PageTitle description="Co-op journeys you have hosted. Progress is saved per room, so the same character can be at a different point in each one.">
        Rooms
      </PageTitle>

      {authLoading || isPending ? (
        <SkeletonList rows={4} />
      ) : rooms.length === 0 ? (
        <EmptyState
          icon={DoorOpen}
          title="No rooms yet"
          description="Host a co-op session in the game client and it will show up here after the first save."
        />
      ) : (
        <div className="stagger space-y-3">
          {paged.map((room, i) => (
            <div key={room.id} style={{ "--i": i } as React.CSSProperties}>
              <RoomCard room={room} />
            </div>
          ))}
          <Pagination page={page} totalPages={totalPages} onChange={setPage} />
        </div>
      )}
    </PageShell>
  );
}

function RoomCard({ room }: { room: SessionSummaryDto }) {
  return (
    <Card interactive className="p-0">
      <Link href={`/rooms/${room.id}`} className="flex items-center gap-4 p-4">
        <div className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-accent-soft font-mono text-sm font-bold text-accent">
          {room.roomCode}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <h3 className="truncate font-medium text-fg">{room.name}</h3>
            <span className="flex items-center gap-1 rounded-full bg-surface-3 px-2 py-0.5 text-xs text-fg-muted">
              <Users size={11} /> {room.characterCount}
            </span>
          </div>
          <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-fg-muted">
            {room.currentScene && (
              <span className="flex items-center gap-1">
                <Map size={12} /> {room.currentScene}
              </span>
            )}
            <span className="flex items-center gap-1">
              <Clock size={12} /> {formatPlaytime(room.playTimeSeconds)}
            </span>
            <span>
              Last played <RelativeTime iso={room.lastPlayedAt} />
            </span>
          </div>
        </div>
        <ChevronRight size={18} className="shrink-0 text-fg-subtle" />
      </Link>
    </Card>
  );
}
