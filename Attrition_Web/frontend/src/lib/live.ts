/**
 * Live-update cadences for TanStack Query.
 *
 * The site refreshes by polling rather than pushing. There is no SignalR anywhere in the backend,
 * and adding it would mean a hub per service across a schema-per-service split, JWT over
 * websockets, a gateway route per hub, and a publish call at every write site — for data where
 * a few seconds of staleness is unnoticeable. Polling gets every page live for a fraction of the
 * work and no new infrastructure. The seam is narrow: swapping in push later means replacing
 * these intervals with subscriptions that invalidate the same query keys.
 *
 * Pick by how fast the data actually changes and how many people are watching:
 *
 * - LIVE_FAST   — a conversation in progress: someone is waiting on the next reply.
 * - LIVE_NORMAL — lists and admin tables: changes matter but nobody is watching the clock.
 * - LIVE_SLOW   — reference content that changes a few times a day.
 *
 * Requests only fire while the tab is in the foreground: TanStack Query pauses timers on hidden
 * documents, so a backgrounded tab costs nothing and catches up on focus.
 */

/** Live conversation — forum thread posts, notification bell. */
export const LIVE_FAST = 5_000;

/** Lists, dashboards, admin tables. */
export const LIVE_NORMAL = 20_000;

/** Wiki articles, music library, bestiary — slow-moving reference data. */
export const LIVE_SLOW = 60_000;

/**
 * Poll only while the tab is focused *and* the window is visible.
 *
 * Pass as `refetchInterval`. TanStack Query already skips refetches for a hidden document; this
 * also stops a visible-but-unfocused window (a tiled second monitor) from polling at the fast
 * cadence all day.
 */
export function liveWhenFocused(interval: number) {
  return () => (typeof document !== "undefined" && document.hasFocus() ? interval : false);
}
