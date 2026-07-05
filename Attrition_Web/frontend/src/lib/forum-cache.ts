import type { ForumPostDto, PaginatedResponse, UserDto } from "@/lib/types";

/**
 * Optimistic-cache helpers shared by the forum thread page and wiki comments. Both render a
 * paginated list of ForumPostDto and mutate it the same way (add a reply, toggle a reaction), so
 * the logic lives here once. The pattern: apply instantly to the cache, then reconcile with the
 * server's response (or roll back on failure).
 */

export type PostsPage = PaginatedResponse<ForumPostDto> | undefined;

/**
 * A stand-in post shown the instant a reply is submitted. Its id is temporary and gets swapped for
 * the server's real post on success (so reactions/links use the true id) or rolled back on error.
 */
export function makeOptimisticPost(opts: {
  threadId: string;
  content: string;
  parentPostId: string | null;
  attachments?: string[];
  user: UserDto | null;
}): ForumPostDto {
  return {
    id: `optimistic-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`,
    threadId: opts.threadId,
    parentPostId: opts.parentPostId,
    depth: 0,
    authorId: opts.user?.id ?? "",
    authorName: opts.user?.username ?? "You",
    authorAvatar: opts.user?.avatarUrl ?? null,
    authorRole: opts.user?.role === "Admin" ? "Admin" : "User",
    content: opts.content,
    attachments: opts.attachments ?? [],
    createdAt: new Date().toISOString(),
    updatedAt: null,
    likeCount: 0,
    dislikeCount: 0,
    currentUserReaction: null,
  };
}

/** Append a post to a cached page and bump the count (optimistic add). */
export function addPostToPage(page: PostsPage, post: ForumPostDto): PostsPage {
  if (!page) return page;
  return { ...page, items: [...page.items, post], totalCount: page.totalCount + 1 };
}

/** Replace a post (matched by id) — used to swap a temp optimistic post for the server's real one. */
export function replacePostInPage(page: PostsPage, matchId: string, next: ForumPostDto): PostsPage {
  if (!page) return page;
  return { ...page, items: page.items.map((p) => (p.id === matchId ? next : p)) };
}

/**
 * Remove a post (by id) from a cached page and drop the count (optimistic delete). The backend
 * removes only that single post; any child replies get re-parented to top-level by buildTree, so
 * dropping just this item mirrors the server exactly.
 */
export function removePostFromPage(page: PostsPage, postId: string): PostsPage {
  if (!page) return page;
  return { ...page, items: page.items.filter((p) => p.id !== postId), totalCount: Math.max(0, page.totalCount - 1) };
}

/** Toggle a like/dislike on a post in a cached page, mirroring the server's toggle semantics. */
export function applyReactionToPage(page: PostsPage, postId: string, type: "like" | "dislike"): PostsPage {
  if (!page) return page;
  return {
    ...page,
    items: page.items.map((p) => {
      if (p.id !== postId) return p;
      let { likeCount, dislikeCount } = p;
      if (p.currentUserReaction === "like") likeCount--;
      if (p.currentUserReaction === "dislike") dislikeCount--;
      const next = p.currentUserReaction === type ? null : type;
      if (next === "like") likeCount++;
      if (next === "dislike") dislikeCount++;
      return { ...p, currentUserReaction: next, likeCount, dislikeCount };
    }),
  };
}

/** First reply/comment page size — shared so thread-create seeding matches the thread page's key. */
export const FORUM_REPLY_PAGE_SIZE = 50;
