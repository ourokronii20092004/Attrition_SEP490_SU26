/**
 * Notification link parsing.
 *
 * Notifications carry a deep link rather than structured ids (`/forum/{threadId}#post-{postId}`),
 * so the thread a notification came from is recovered from that link. Keeping it here — as pure
 * string handling — means the notification list and the bell can both offer a "mute this thread"
 * action without either one guessing at the URL shape.
 */

/** Canonical GUID form, as the API emits it in links. */
const THREAD_LINK = /^\/forum\/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})(?:[/#?]|$)/;

/**
 * The forum thread a notification points at, or null when it points somewhere else.
 *
 * Returning null is the signal that muting isn't offered for that row — a mention on a profile or
 * any future notification type shouldn't grow a thread-mute button by accident.
 */
export function threadIdFromNotificationLink(link: string | null | undefined): string | null {
  if (!link) return null;
  const match = THREAD_LINK.exec(link.trim());
  return match ? match[1] : null;
}
