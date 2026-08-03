import type { ForumPostDto } from "@/lib/types";

/** A post plus its direct replies. */
export type PostNode = ForumPostDto & { children: PostNode[] };

/**
 * How many indented layers a reply thread may show: level 0 (a top-level reply), 1, and 2.
 * Replies deeper than that still thread and stay in order — they just render flush with level 2
 * instead of indenting further, so long conversations can't walk off the right edge on mobile.
 *
 * The server caps *stored* depth separately (ForumService.MaxDepth = 8) and may differ freely;
 * this constant alone governs indentation.
 */
export const MAX_VISUAL_DEPTH = 3;

/** Deepest level that still gets its own indent step. */
export const LAST_INDENT_LEVEL = MAX_VISUAL_DEPTH - 1;

/**
 * Whether a node at `level` should indent its replies.
 *
 * Children sit at `level + 1`, and only levels up to `LAST_INDENT_LEVEL` are indented, so a node
 * indents while `level < LAST_INDENT_LEVEL`. Past that, children render flush — visually joining
 * their grandparent's column rather than starting a new one.
 */
export function indentsChildren(level: number): boolean {
  return level < LAST_INDENT_LEVEL;
}

/**
 * Build a reply tree from the flat, chronological post list.
 * Orphans (parent missing or removed) fall back to top-level so nothing is hidden.
 */
export function buildTree(posts: ForumPostDto[]): PostNode[] {
  const byId = new Map<string, PostNode>();
  for (const post of posts) byId.set(post.id, { ...post, children: [] });
  const roots: PostNode[] = [];
  for (const node of byId.values()) {
    const parent = node.parentPostId ? byId.get(node.parentPostId) : null;
    if (parent) parent.children.push(node);
    else roots.push(node);
  }
  return roots;
}
