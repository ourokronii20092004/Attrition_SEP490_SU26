"use client";

import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { ArrowLeft, ListMusic, Plus, Trash2, ChevronRight } from "lucide-react";
import { musicApi } from "@/lib/api/music";
import { useAuth, useToast, useConfirm } from "@/lib/providers";
import { PageShell } from "@/components/ui/page-shell";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { SkeletonList } from "@/components/ui/skeleton";
import { EmptyState } from "@/components/ui/empty-state";
import { qk } from "@/lib/query-keys";
import { useLoginHref } from "@/lib/hooks/use-login-href";

export default function PlaylistsPage() {
  const loginHref = useLoginHref();
  const { user } = useAuth();
  const { toast } = useToast();
  const confirm = useConfirm();
  const queryClient = useQueryClient();
  const [creating, setCreating] = useState(false);
  const [name, setName] = useState("");
  const [busy, setBusy] = useState(false);

  const { data: playlists = [], isPending } = useQuery({
    queryKey: qk.music.playlists(),
    enabled: !!user,
    queryFn: async () => {
      const res = await musicApi.getPlaylists();
      return res.success ? res.data ?? [] : [];
    },
  });

  const create = async () => {
    if (!name.trim()) return;
    setBusy(true);
    try {
      const res = await musicApi.createPlaylist({ name: name.trim() });
      if (res.success) {
        toast("Playlist created.", "success");
        setName("");
        setCreating(false);
        queryClient.invalidateQueries({ queryKey: qk.music.playlists() });
      } else {
        toast(res.error || "Couldn't create playlist.", "error");
      }
    } catch {
      toast("Couldn't create playlist.", "error");
    } finally {
      setBusy(false);
    }
  };

  const remove = async (id: string, title: string) => {
    const ok = await confirm({
      title: "Delete playlist?",
      message: `"${title}" and its track list will be permanently removed. This can't be undone.`,
      confirmLabel: "Delete",
      danger: true,
    });
    if (!ok) return;
    const res = await musicApi.deletePlaylist(id);
    if (res.success) {
      toast("Playlist deleted.", "success");
      queryClient.invalidateQueries({ queryKey: qk.music.playlists() });
    } else {
      toast(res.error || "Couldn't delete playlist.", "error");
    }
  };

  if (!user) {
    return (
      <PageShell>
        <EmptyState
          icon={ListMusic}
          title="Sign in to see your playlists"
          description="Create playlists and add tracks you love."
          action={<Link href={loginHref}><Button variant="secondary">Sign in</Button></Link>}
        />
      </PageShell>
    );
  }

  return (
    <PageShell>
      <Link href="/music" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Music
      </Link>

      <div className="mt-4 flex items-end justify-between gap-4">
        <div>
          <h1 className="font-display text-3xl font-bold tracking-tight text-fg sm:text-4xl">My Playlists</h1>
          <p className="mt-1 text-sm text-fg-muted">{playlists.length} {playlists.length === 1 ? "playlist" : "playlists"}</p>
        </div>
        {!creating && (
          <Button size="sm" onClick={() => setCreating(true)}><Plus size={16} className="mr-1.5" /> New playlist</Button>
        )}
      </div>

      {creating && (
        <div className="mt-4 flex flex-wrap items-end gap-2 rounded-card border border-border bg-surface p-3">
          <div className="min-w-56 flex-1">
            <Input
              label="Playlist name" autoFocus value={name} maxLength={100}
              onChange={(e) => setName(e.target.value)}
              onKeyDown={(e) => { if (e.key === "Enter") create(); }}
              placeholder="e.g. Boss fight anthems"
            />
          </div>
          <Button onClick={create} loading={busy} disabled={!name.trim()}>Create</Button>
          <Button variant="secondary" onClick={() => { setCreating(false); setName(""); }}>Cancel</Button>
        </div>
      )}

      {isPending ? (
        <SkeletonList rows={5} className="mt-6" />
      ) : playlists.length === 0 ? (
        <EmptyState
          icon={ListMusic}
          title="No playlists yet"
          description="Create your first playlist, then add tracks from any album or the player."
          className="mt-6"
        />
      ) : (
        <div className="mt-6 space-y-2">
          {playlists.map((pl) => (
            <div key={pl.playlistId} className="group flex items-center gap-3 rounded-card border border-border bg-surface p-4 transition-colors hover:border-accent/50">
              <span className="flex h-11 w-11 shrink-0 items-center justify-center rounded-md bg-surface-2 text-accent">
                <ListMusic size={20} />
              </span>
              <Link href={`/music/playlists/${pl.playlistId}`} className="min-w-0 flex-1">
                <span className="block truncate font-medium text-fg transition-colors group-hover:text-accent">{pl.title}</span>
                <span className="block text-xs text-fg-muted">{pl.trackCount} {pl.trackCount === 1 ? "track" : "tracks"}</span>
              </Link>
              <button onClick={() => remove(pl.playlistId, pl.title)}
                className="shrink-0 rounded-md p-2 text-fg-subtle transition-colors hover:bg-surface-2 hover:text-danger"
                aria-label={`Delete ${pl.title}`}>
                <Trash2 size={16} />
              </button>
              <Link href={`/music/playlists/${pl.playlistId}`} className="shrink-0 rounded-md p-2 text-fg-subtle transition-colors hover:bg-surface-2 hover:text-fg" aria-label={`Open ${pl.title}`}>
                <ChevronRight size={16} />
              </Link>
            </div>
          ))}
        </div>
      )}
    </PageShell>
  );
}
