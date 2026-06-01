/**
 * Formats a duration in seconds as "m:ss" (e.g. 83 -> "1:23").
 * Returns "0:00" for non-finite input (NaN duration before metadata loads).
 * Shared by the audio player, now-playing bar, and queue panel.
 */
export function formatDuration(seconds: number): string {
  if (!Number.isFinite(seconds)) return "0:00";
  return `${Math.floor(seconds / 60)}:${String(Math.floor(seconds % 60)).padStart(2, "0")}`;
}
