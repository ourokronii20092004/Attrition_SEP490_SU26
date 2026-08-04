/**
 * Formats a duration in seconds as "m:ss" (e.g. 83 -> "1:23").
 * Returns "0:00" for non-finite input (NaN duration before metadata loads).
 * Shared by the audio player, now-playing bar, and queue panel.
 */
export function formatDuration(seconds: number): string {
  if (!Number.isFinite(seconds)) return "0:00";
  return `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, "0")}`;
}

/**
 * Formats a playtime in seconds as "3h 24m" (or "24m" under an hour). Used for game playtime,
 * which runs to hours — `formatDuration` above would render that as "204:00".
 */
export function formatPlaytime(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds < 0) return "0m";
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}
