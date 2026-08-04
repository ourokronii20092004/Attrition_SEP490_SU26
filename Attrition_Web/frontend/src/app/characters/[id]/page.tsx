"use client";

import { Suspense, useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Gamepad2, History, TriangleAlert } from "lucide-react";
import { charactersApi } from "@/lib/api/characters";
import { sessionsApi } from "@/lib/api/sessions";
import { useAuth, useConfirm, useToast } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { BackButton } from "@/components/ui/back-button";
import { Card } from "@/components/ui/card";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { Pagination } from "@/components/ui/pagination";
import { SaveRail } from "@/components/save-rail";
import { SaveDetailPanel } from "@/components/save-detail-panel";
import { qk } from "@/lib/query-keys";
import { useUrlPage } from "@/lib/hooks/use-url-pagination";
import { formatPlaytime } from "@/lib/format-duration";
import type { SaveListItemDto } from "@/lib/types";

const SAVES_PER_PAGE = 12;

function CharacterDetail() {
  const params = useParams<{ id: string }>();
  const characterId = params.id;
  const { user } = useAuth();
  const { toast } = useToast();
  const confirm = useConfirm();
  const queryClient = useQueryClient();

  const [selectedId, setSelectedId] = useState<number | null>(null);

  const { data: character, isPending: charPending } = useQuery({
    queryKey: qk.characters.detail(characterId),
    enabled: !!characterId,
    queryFn: async () => {
      const res = await charactersApi.get(characterId);
      return res.success ? res.data : null;
    },
  });

  // Saves are paged independently of the character record, so deleting one refetches just this list.
  // useUrlPage rather than useUrlPagination: the server does the slicing, and the client-side
  // variant would clamp the page against a list it hasn't got yet.
  const [page, setPage] = useUrlPage(undefined, "sp");
  const { data: saveList, isPending: savesPending } = useQuery({
    queryKey: qk.characters.saves(characterId, page),
    enabled: !!characterId,
    queryFn: async () => {
      const res = await charactersApi.getSaves(characterId, { page, pageSize: SAVES_PER_PAGE });
      return res.success ? res.data : null;
    },
  });

  const saves = saveList?.items ?? [];
  const savesTotalPages = Math.max(1, Math.ceil((saveList?.totalCount ?? 0) / SAVES_PER_PAGE));

  // Default to the newest save — the character's current progress — rather than nothing.
  useEffect(() => {
    if (selectedId === null && saves.length > 0) setSelectedId(saves[0].id);
  }, [saves, selectedId]);

  const { data: selectedSave, isPending: savePending } = useQuery({
    queryKey: qk.characters.save(characterId, selectedId ?? 0),
    enabled: !!characterId && selectedId !== null,
    queryFn: async () => {
      const res = await charactersApi.getSave(characterId, selectedId!);
      return res.success ? res.data : null;
    },
  });

  // The room's owner may additionally roll the room's shared world back. Only fetched when the
  // selected save belongs to a room, since that is the only case where the option is offered.
  const { data: room } = useQuery({
    queryKey: qk.sessions.detail(selectedSave?.sessionId ?? ""),
    enabled: !!selectedSave?.sessionId,
    queryFn: async () => {
      const res = await sessionsApi.get(selectedSave!.sessionId!);
      return res.success ? res.data : null;
    },
  });

  const isRoomOwner = !!room && !!user && room.ownerId === user.id;

  const deleteMutation = useMutation({
    mutationFn: ({ saveId, rollWorld }: { saveId: number; rollWorld: boolean }) =>
      charactersApi.deleteSave(characterId, saveId, rollWorld),
    onSuccess: (res) => {
      const d = res.data;
      queryClient.invalidateQueries({ queryKey: qk.characters.saves(characterId) });
      queryClient.invalidateQueries({ queryKey: qk.characters.detail(characterId) });
      setSelectedId(null); // re-defaults to the new newest save

      if (!d) {
        toast("Save deleted.", "success");
        return;
      }
      if (d.rolledBackCharacter) {
        const world = d.rolledBackWorldState ? " Room progress was rolled back too." : "";
        toast(`Save deleted. Your progress rolled back to the previous save.${world}`, "success");
      } else {
        toast("Save deleted.", "success");
      }
    },
    onError: () => toast("Couldn't delete that save. Please try again.", "error"),
  });

  const totalSaves = saveList?.totalCount ?? 0;

  const handleDelete = async (save: SaveListItemDto) => {
    // Deleting the newest save is the destructive case: it is the state the game loads, so removing
    // it really does undo progress. Anything older only prunes history.
    if (!save.isCurrent) {
      const ok = await confirm({
        title: "Delete this save file?",
        message:
          `This removes the save from ${new Date(save.capturedAt).toLocaleString()} from your history. ` +
          `Your current progress is not affected.`,
        confirmLabel: "Delete save",
      });
      if (ok) deleteMutation.mutate({ saveId: save.id, rollWorld: false });
      return;
    }

    const previous = saves.find((s) => s.id !== save.id);
    const lostLevels = previous ? save.currentLevel - previous.currentLevel : 0;
    const lostTime = previous ? save.playtimeSeconds - previous.playtimeSeconds : 0;

    const lost: string[] = [];
    if (lostLevels > 0) lost.push(`${lostLevels} level${lostLevels === 1 ? "" : "s"}`);
    if (lostTime > 0) lost.push(formatPlaytime(lostTime));

    const ok = await confirm({
      title: "Delete your current progress?",
      message:
        `This is your newest save — the one the game loads. Deleting it rolls your character back to ` +
        `${previous ? new Date(previous.capturedAt).toLocaleString() : "the previous save"}` +
        (lost.length ? `, losing ${lost.join(" and ")} of progress` : "") +
        `. This cannot be undone.` +
        (isRoomOwner && save.sessionId
          ? ` Room progress (bosses, quests, map) is kept unless you choose otherwise below.`
          : ""),
      confirmLabel: "Roll back my progress",
      danger: true,
    });
    if (!ok) return;

    // Rolling the room back is a second, separate decision: it rewrites progress for everyone in
    // the room, so it is never bundled into the first confirmation.
    let rollWorld = false;
    if (isRoomOwner && save.sessionId) {
      const others = room?.characters?.filter((c) => c.characterId !== characterId) ?? [];
      rollWorld = await confirm({
        title: "Also roll back room progress?",
        message:
          others.length > 0
            ? `You own room ${room?.roomCode}. Rolling its progress back also undoes bosses, quests and ` +
              `map discovery for ${others.length} other character${others.length === 1 ? "" : "s"} in it. ` +
              `Choose Keep to roll back only your own character.`
            : `This also undoes bosses defeated, quests and map discovery in room ${room?.roomCode}. ` +
              `Choose Keep to roll back only your character.`,
        confirmLabel: "Roll back room too",
        danger: true,
      });
    }

    deleteMutation.mutate({ saveId: save.id, rollWorld });
  };

  if (charPending) {
    return (
      <PageShell size="lg">
        <div className="mb-5"><BackButton fallbackHref="/characters" label="Back to characters" /></div>
        <Skeleton className="h-10 w-1/2" />
        <Skeleton className="mt-4 h-40 w-full" />
      </PageShell>
    );
  }

  if (!character) {
    return (
      <PageShell size="lg">
        <div className="mb-5"><BackButton fallbackHref="/characters" label="Back to characters" /></div>
        <EmptyState icon={Gamepad2} title="Character not found"
          description="This character doesn't exist, or it isn't yours." />
      </PageShell>
    );
  }

  return (
    <PageShell size="lg">
      <div className="mb-5"><BackButton fallbackHref="/characters" label="Back to characters" /></div>

      <div className="flex flex-wrap items-center gap-3">
        <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-accent-soft font-display text-2xl font-bold text-accent">
          {character.name[0]?.toUpperCase() ?? "?"}
        </div>
        <div className="min-w-0">
          <h1 className="font-display text-3xl font-bold tracking-tight text-fg">{character.name}</h1>
          <p className="text-sm text-fg-muted">
            {character.archetype} · {totalSaves} save{totalSaves === 1 ? "" : "s"}
          </p>
        </div>
      </div>

      <div className="mt-8 grid gap-6 lg:grid-cols-[20rem_1fr]">
        {/* ── Save rail ── */}
        <aside className="min-w-0">
          <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-fg">
            <History size={17} aria-hidden /> Save files
          </h2>
          <p className="mt-1 text-xs text-fg-subtle">
            Pick one to see the character as it was then. Newest is what the game loads.
          </p>

          {savesPending ? (
            <div className="mt-3 space-y-2">
              {[0, 1, 2].map((i) => <Skeleton key={i} className="h-16 w-full rounded-lg" />)}
            </div>
          ) : saves.length === 0 ? (
            <Card className="mt-3 p-4 text-sm text-fg-muted">
              No saves recorded yet. They appear here after you rest or quit in game.
            </Card>
          ) : (
            <div className="mt-3">
              <SaveRail
                saves={saves}
                selectedId={selectedId}
                onSelect={setSelectedId}
                onDelete={handleDelete}
                deletingId={deleteMutation.isPending ? deleteMutation.variables?.saveId ?? null : null}
                canDelete={totalSaves > 1}
              />
              {savesTotalPages > 1 && (
                <Pagination page={page} totalPages={savesTotalPages} onChange={setPage} compact />
              )}
              {totalSaves === 1 && (
                <p className="mt-3 flex items-start gap-1.5 text-xs text-fg-subtle">
                  <TriangleAlert size={12} className="mt-0.5 shrink-0" aria-hidden />
                  A character keeps at least one save, so this one can&apos;t be deleted.
                </p>
              )}
            </div>
          )}
        </aside>

        {/* ── Selected save ── */}
        <div className="min-w-0">
          {savePending && selectedId !== null ? (
            <div className="space-y-4">
              <Skeleton className="h-32 w-full" />
              <Skeleton className="h-24 w-full" />
            </div>
          ) : selectedSave ? (
            <SaveDetailPanel save={selectedSave} />
          ) : (
            <Card className="p-6 text-center text-sm text-fg-muted">
              Select a save file to see its details.
            </Card>
          )}
        </div>
      </div>
    </PageShell>
  );
}

export default function CharacterDetailPage() {
  return (
    <Suspense fallback={null}>
      <CharacterDetail />
    </Suspense>
  );
}
