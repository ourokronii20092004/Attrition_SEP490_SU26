"use client";

import { Suspense, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Gamepad2, History, User, TriangleAlert, Activity, Clock } from "lucide-react";
import { charactersApi } from "@/lib/api/characters";
import { adminApi } from "@/lib/api/admin";
import { useAuth, useConfirm, useToast } from "@/lib/providers";
import { Card } from "@/components/ui/card";
import { PageLoader } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { Skeleton } from "@/components/ui/skeleton";
import { Pagination } from "@/components/ui/pagination";
import { BackButton } from "@/components/ui/back-button";
import { SaveRail } from "@/components/save-rail";
import { SaveDetailPanel } from "@/components/save-detail-panel";
import { CopyButton, JsonDisclosure } from "@/components/admin/copy-button";
import { RelativeTime } from "@/components/ui/relative-time";
import { formatPlaytime } from "@/lib/format-duration";
import { formatDateTime } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";
import { useAdminPageLabel } from "@/lib/hooks/use-admin-page-label";
import { useUrlPage } from "@/lib/hooks/use-url-pagination";
import type { SaveListItemDto } from "@/lib/types";

const SAVES_PER_PAGE = 12;

/**
 * How suspicious the jump between two consecutive saves looks.
 *
 * This answers the question an admin actually has — "is this progression plausible?" — from data
 * already stored, rather than showing raw ids. A gap is only *worth a look*, never proof: a long
 * session or a co-op carry can legitimately produce a big jump.
 */
function describeJump(newer: SaveListItemDto, older: SaveListItemDto): string | null {
  const levels = newer.currentLevel - older.currentLevel;
  const minutes = Math.max(0, (newer.playtimeSeconds - older.playtimeSeconds) / 60);
  if (levels >= 5 && minutes < 10) {
    return `${levels} levels in ${minutes < 1 ? "under a minute" : `${Math.round(minutes)} min`}`;
  }
  if (levels >= 10) return `${levels} levels in one save`;
  return null;
}

function AdminCharacterDetail() {
  const params = useParams<{ id: string }>();
  const characterId = params.id;
  const { user } = useAuth();
  const { toast } = useToast();
  const confirm = useConfirm();
  const queryClient = useQueryClient();

  const [selectedId, setSelectedId] = useState<number | null>(null);

  const { data: detail, isPending } = useQuery({
    queryKey: qk.admin.character(characterId),
    enabled: user?.role === "Admin" && !!characterId,
    queryFn: async () => {
      const res = await charactersApi.getAdmin(characterId);
      return res.success ? res.data : null;
    },
  });

  useAdminPageLabel(detail?.name);

  // Owner identity — the first thing an admin wants: whose character is this?
  const { data: owner } = useQuery({
    queryKey: qk.admin.userDetail(detail?.ownerId ?? ""),
    enabled: user?.role === "Admin" && !!detail?.ownerId,
    queryFn: async () => {
      const res = await adminApi.getUserDetail(detail!.ownerId);
      return res.success ? res.data : null;
    },
  });

  const [page, setPage] = useUrlPage(undefined, "sp");
  const { data: saveList, isPending: savesPending } = useQuery({
    queryKey: qk.characters.saves(characterId, page),
    enabled: user?.role === "Admin" && !!characterId,
    queryFn: async () => {
      const res = await charactersApi.getSaves(characterId, { page, pageSize: SAVES_PER_PAGE });
      return res.success ? res.data : null;
    },
  });

  const saves = saveList?.items ?? [];
  const totalSaves = saveList?.totalCount ?? 0;
  const savesTotalPages = Math.max(1, Math.ceil(totalSaves / SAVES_PER_PAGE));

  useEffect(() => {
    if (selectedId === null && saves.length > 0) setSelectedId(saves[0].id);
  }, [saves, selectedId]);

  const { data: selectedSave, isPending: savePending } = useQuery({
    queryKey: qk.characters.save(characterId, selectedId ?? 0),
    enabled: user?.role === "Admin" && !!characterId && selectedId !== null,
    queryFn: async () => {
      const res = await charactersApi.getSave(characterId, selectedId!);
      return res.success ? res.data : null;
    },
  });

  // Progression anomalies across the visible page of saves.
  const jumps = useMemo(() => {
    const out: { save: SaveListItemDto; note: string }[] = [];
    for (let i = 0; i < saves.length - 1; i++) {
      const note = describeJump(saves[i], saves[i + 1]);
      if (note) out.push({ save: saves[i], note });
    }
    return out;
  }, [saves]);

  const deleteMutation = useMutation({
    mutationFn: (saveId: number) => charactersApi.deleteSave(characterId, saveId, false),
    onSuccess: (res) => {
      queryClient.invalidateQueries({ queryKey: qk.characters.saves(characterId) });
      queryClient.invalidateQueries({ queryKey: qk.admin.character(characterId) });
      setSelectedId(null);
      toast(
        res.data?.rolledBackCharacter
          ? "Save deleted. The player's progress was rolled back to the previous save."
          : "Save deleted.",
        "success",
      );
    },
    onError: () => toast("Couldn't delete that save. Please try again.", "error"),
  });

  const handleDelete = async (save: SaveListItemDto) => {
    const who = owner?.username ?? "this player";
    // Worded as acting on someone else's data, because that is what an admin is doing here.
    const ok = await confirm({
      title: save.isCurrent ? "Delete this player's current progress?" : "Delete this save file?",
      message: save.isCurrent
        ? `This is ${who}'s newest save — the one their game loads. Deleting it rolls their character ` +
          `back to the previous save. They are not notified, and this cannot be undone.`
        : `This removes a save from ${who}'s history. Their current progress is not affected.`,
      confirmLabel: save.isCurrent ? "Roll their progress back" : "Delete save",
      danger: save.isCurrent,
    });
    if (ok) deleteMutation.mutate(save.id);
  };

  if (!user || user.role !== "Admin") return null;
  if (isPending) return <PageLoader />;

  if (!detail) {
    return (
      <div>
        <div className="mb-5"><BackButton fallbackHref="/admin/characters" label="Back to characters" /></div>
        <EmptyState icon={Gamepad2} title="Character not found" description="It may have been deleted." />
      </div>
    );
  }

  return (
    <div>
      <div className="mb-5"><BackButton fallbackHref="/admin/characters" label="Back to characters" /></div>

      {/* ── Identity ── */}
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-center gap-3">
          <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-xl bg-accent-soft font-display text-xl font-bold text-accent">
            {detail.name[0]?.toUpperCase() ?? "?"}
          </span>
          <div className="min-w-0">
            <h1 className="font-display text-2xl font-bold tracking-tight text-fg">{detail.name}</h1>
            <p className="flex flex-wrap items-center gap-x-2 text-sm text-fg-muted">
              <span>{detail.archetype}</span>
              <span aria-hidden>·</span>
              <span>{totalSaves} save{totalSaves === 1 ? "" : "s"}</span>
            </p>
          </div>
        </div>
        <div className="flex items-center gap-1 text-xs text-fg-subtle">
          <span>Character ID</span>
          <CopyButton value={detail.id} label="Character ID" />
        </div>
      </div>

      {/* ── Owner ── */}
      <Card className="mt-5 p-4">
        <h2 className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-fg-subtle">
          <User size={12} aria-hidden /> Owner
        </h2>
        <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
          {owner ? (
            <>
              <Link href={`/u/${encodeURIComponent(owner.username)}`}
                className="font-medium text-fg transition-colors hover:text-accent">
                {owner.username}
              </Link>
              {owner.isBanned && (
                <span className="rounded-full bg-danger/10 px-2 py-0.5 text-xs font-medium text-danger">Banned</span>
              )}
              <Link href={`/admin/users/${detail.ownerId}`}
                className="text-xs text-fg-muted underline-offset-2 hover:text-accent hover:underline">
                Manage user
              </Link>
            </>
          ) : (
            <span className="text-fg-muted">Unknown player</span>
          )}
          <span className="ml-auto flex items-center gap-1 text-xs text-fg-subtle">
            Owner ID <CopyButton value={detail.ownerId} label="Owner ID" />
          </span>
        </div>
        <dl className="mt-3 grid gap-3 border-t border-border pt-3 text-xs sm:grid-cols-3">
          <div>
            <dt className="text-fg-subtle">Created</dt>
            <dd className="mt-0.5 text-fg">{formatDateTime(detail.createdAt)}</dd>
          </div>
          <div>
            <dt className="text-fg-subtle">Last updated</dt>
            <dd className="mt-0.5 text-fg">{formatDateTime(detail.updatedAt)}</dd>
          </div>
          <div>
            <dt className="text-fg-subtle">Saves recorded</dt>
            <dd className="mt-0.5 text-fg">{totalSaves}</dd>
          </div>
        </dl>
      </Card>

      {/* ── Progression anomalies: the moderation signal, not raw plumbing ── */}
      {jumps.length > 0 && (
        <Card className="mt-4 border-warning/40 bg-warning/5 p-4">
          <h2 className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-warning">
            <Activity size={12} aria-hidden /> Worth a look
          </h2>
          <ul className="mt-2 space-y-1 text-sm text-fg-muted">
            {jumps.map(({ save, note }) => (
              <li key={save.id} className="flex flex-wrap items-center gap-2">
                <button type="button" onClick={() => setSelectedId(save.id)}
                  className="font-medium text-fg underline-offset-2 hover:text-accent hover:underline">
                  {note}
                </button>
                <span className="text-xs text-fg-subtle">
                  <RelativeTime iso={save.capturedAt} />
                </span>
              </li>
            ))}
          </ul>
          <p className="mt-2 text-xs text-fg-subtle">
            A long session or a co-op carry can produce this legitimately — it is a prompt to check,
            not a verdict.
          </p>
        </Card>
      )}

      <div className="mt-6 grid gap-6 lg:grid-cols-[20rem_1fr]">
        {/* ── Save rail ── */}
        <aside className="min-w-0">
          <h2 className="flex items-center gap-2 font-display text-lg font-semibold text-fg">
            <History size={17} aria-hidden /> Save files
          </h2>
          <p className="mt-1 text-xs text-fg-subtle">
            Newest is what the player&apos;s game loads. Deleting it rolls their character back.
          </p>

          {savesPending ? (
            <div className="mt-3 space-y-2">
              {[0, 1, 2].map((i) => <Skeleton key={i} className="h-16 w-full rounded-lg" />)}
            </div>
          ) : saves.length === 0 ? (
            <Card className="mt-3 p-4 text-sm text-fg-muted">No saves recorded for this character.</Card>
          ) : (
            <div className="mt-3">
              <SaveRail
                saves={saves}
                selectedId={selectedId}
                onSelect={setSelectedId}
                onDelete={handleDelete}
                deletingId={deleteMutation.isPending ? deleteMutation.variables ?? null : null}
                canDelete={totalSaves > 1}
              />
              {savesTotalPages > 1 && (
                <Pagination page={page} totalPages={savesTotalPages} onChange={setPage} compact />
              )}
              {totalSaves === 1 && (
                <p className="mt-3 flex items-start gap-1.5 text-xs text-fg-subtle">
                  <TriangleAlert size={12} className="mt-0.5 shrink-0" aria-hidden />
                  A character keeps at least one save.
                </p>
              )}
            </div>
          )}

          {/* Playtime summary from the visible page — cheap context an admin can sanity-check. */}
          {saves.length > 1 && (
            <Card className="mt-4 p-3 text-xs">
              <p className="flex items-center gap-1.5 font-medium text-fg-muted">
                <Clock size={12} aria-hidden /> Cadence
              </p>
              <p className="mt-1 text-fg-subtle">
                {saves.length} saves shown, spanning{" "}
                {formatPlaytime(Math.max(0, saves[0].playtimeSeconds - saves[saves.length - 1].playtimeSeconds))} of play.
              </p>
            </Card>
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
            <>
              <SaveDetailPanel save={selectedSave} />

              {/* Raw data, behind a disclosure: the rendering above is the answer, this is for when
                  the rendering itself is in question. */}
              <section className="mt-6">
                <h2 className="font-display text-lg font-semibold text-fg">Raw data</h2>
                <div className="mt-2 flex flex-wrap items-center gap-3 text-xs text-fg-subtle">
                  <span className="flex items-center gap-1">
                    Save ID <CopyButton value={String(selectedSave.id)} label="Save ID" />
                  </span>
                  {selectedSave.sessionId && (
                    <>
                      <span className="flex items-center gap-1">
                        Room ID <CopyButton value={selectedSave.sessionId} label="Room ID" />
                      </span>
                      <Link href={`/admin/rooms/${selectedSave.sessionId}`}
                        className="underline-offset-2 hover:text-accent hover:underline">
                        View room
                      </Link>
                    </>
                  )}
                  <span>Role: {selectedSave.playerRole === 0 ? "Host" : "Joined"}</span>
                  <span>Position: {selectedSave.posX.toFixed(1)}, {selectedSave.posY.toFixed(1)}</span>
                </div>
                <JsonDisclosure json={selectedSave.inventoryJson} label="Show inventory JSON" />
                <JsonDisclosure json={selectedSave.allocatedPointsJson} label="Show stat points JSON" />
              </section>
            </>
          ) : (
            <Card className="p-6 text-center text-sm text-fg-muted">Select a save file to see its details.</Card>
          )}
        </div>
      </div>
    </div>
  );
}

export default function AdminCharacterDetailPage() {
  return (
    <Suspense fallback={null}>
      <AdminCharacterDetail />
    </Suspense>
  );
}
