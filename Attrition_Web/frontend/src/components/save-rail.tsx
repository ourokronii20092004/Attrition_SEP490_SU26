"use client";

import { Clock, MapPin, Skull, Trash2 } from "lucide-react";
import { RelativeTime } from "@/components/ui/relative-time";
import { formatPlaytime } from "@/lib/format-duration";
import type { SaveListItemDto } from "@/lib/types";

/**
 * The save-file rail: every save for a character, newest first, one selectable at a time.
 *
 * Selecting a save re-renders the stats beside it, so the page shows the character as it was at
 * that moment. The newest is marked "Current" because it is the state the game will actually load —
 * which is also why deleting it is the destructive case and gets its own warning.
 */
export function SaveRail({
  saves,
  selectedId,
  onSelect,
  onDelete,
  deletingId,
  canDelete,
}: {
  saves: SaveListItemDto[];
  selectedId: number | null;
  onSelect: (saveId: number) => void;
  onDelete?: (save: SaveListItemDto) => void;
  deletingId?: number | null;
  /** False when only one save remains: deleting it would leave the character with no state. */
  canDelete: boolean;
}) {
  return (
    <ul className="space-y-2" aria-label="Save files">
      {saves.map((save) => {
        const selected = save.id === selectedId;
        return (
          <li key={save.id}>
            <div
              className={`group flex items-start gap-2 rounded-lg border p-3 transition-colors ${
                selected
                  ? "border-accent/60 bg-accent-soft/40"
                  : "border-border bg-surface-2/40 hover:border-accent/40"
              }`}
            >
              <button
                type="button"
                onClick={() => onSelect(save.id)}
                aria-pressed={selected}
                className="min-w-0 flex-1 text-left focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
              >
                <div className="flex flex-wrap items-center gap-2">
                  <span className={`text-sm font-medium ${selected ? "text-accent" : "text-fg"}`}>
                    Level {save.currentLevel}
                  </span>
                  {save.isCurrent && (
                    <span className="rounded-full bg-success/10 px-2 py-0.5 text-[11px] font-semibold text-success">
                      Current
                    </span>
                  )}
                  {!save.isAlive && (
                    <span className="inline-flex items-center gap-1 rounded-full bg-danger/10 px-2 py-0.5 text-[11px] font-medium text-danger">
                      <Skull size={10} aria-hidden /> Died
                    </span>
                  )}
                </div>

                <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-0.5 text-xs text-fg-muted">
                  <span className="flex items-center gap-1">
                    <Clock size={11} aria-hidden /> <RelativeTime iso={save.capturedAt} />
                  </span>
                  {save.currentScene && (
                    <span className="flex items-center gap-1 truncate">
                      <MapPin size={11} aria-hidden /> {save.currentScene}
                    </span>
                  )}
                  {save.playtimeSeconds > 0 && <span>{formatPlaytime(save.playtimeSeconds)}</span>}
                </div>
              </button>

              {onDelete && (
                <button
                  type="button"
                  onClick={() => onDelete(save)}
                  disabled={!canDelete || deletingId === save.id}
                  title={
                    canDelete
                      ? save.isCurrent
                        ? "Delete this save — your progress rolls back to the previous one"
                        : "Delete this save file"
                      : "A character must keep at least one save"
                  }
                  aria-label={`Delete save from ${new Date(save.capturedAt).toLocaleString()}`}
                  className="shrink-0 rounded-md p-1.5 text-fg-subtle transition-colors hover:bg-danger/10 hover:text-danger disabled:cursor-not-allowed disabled:opacity-40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-danger"
                >
                  <Trash2 size={15} aria-hidden />
                </button>
              )}
            </div>
          </li>
        );
      })}
    </ul>
  );
}
