import { describe, it, expect } from "vitest";
import { buildTree, indentsChildren, LAST_INDENT_LEVEL, MAX_VISUAL_DEPTH } from "./forum-tree";
import type { ForumPostDto } from "@/lib/types";

/** Minimal post; only the fields the tree logic reads actually matter. */
function post(id: string, parentPostId: string | null, authorName = `user-${id}`): ForumPostDto {
  return {
    id,
    threadId: "t1",
    parentPostId,
    depth: 0,
    authorId: `a-${id}`,
    authorName,
    authorAvatar: null,
    authorRole: "User",
    content: `content ${id}`,
    attachments: [],
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: null,
    likeCount: 0,
    dislikeCount: 0,
    currentUserReaction: null,
  } as unknown as ForumPostDto;
}

describe("buildTree", () => {
  it("nests replies under their parent", () => {
    const tree = buildTree([post("1", null), post("2", "1"), post("3", "2")]);
    expect(tree).toHaveLength(1);
    expect(tree[0].children[0].id).toBe("2");
    expect(tree[0].children[0].children[0].id).toBe("3");
  });

  it("promotes orphans to top level so nothing is hidden", () => {
    const tree = buildTree([post("1", null), post("2", "missing-parent")]);
    expect(tree.map((n) => n.id).sort()).toEqual(["1", "2"]);
  });

  it("keeps sibling replies in the order the API returned them", () => {
    const tree = buildTree([post("1", null), post("2", "1"), post("3", "1"), post("4", "1")]);
    expect(tree[0].children.map((n) => n.id)).toEqual(["2", "3", "4"]);
  });

  it("keeps every post in the tree", () => {
    const posts = [post("1", null), post("2", "1"), post("3", "2"), post("4", "3"), post("5", "4")];
    const count = (nodes: ReturnType<typeof buildTree>): number =>
      nodes.reduce((sum, n) => sum + 1 + count(n.children), 0);
    expect(count(buildTree(posts))).toBe(5);
  });
});

describe("indentsChildren", () => {
  it("indents the first levels then stops, giving exactly 3 visible layers", () => {
    expect(indentsChildren(0)).toBe(true); // level 0 -> children at level 1
    expect(indentsChildren(1)).toBe(true); // level 1 -> children at level 2
    expect(indentsChildren(2)).toBe(false); // level 2 is the last indent; deeper renders flush
  });

  it("never indents past the cap no matter how deep the thread goes", () => {
    for (const level of [LAST_INDENT_LEVEL, LAST_INDENT_LEVEL + 1, 7, 42]) {
      expect(indentsChildren(level)).toBe(false);
    }
  });

  it("derives the last indent level from the depth cap", () => {
    expect(LAST_INDENT_LEVEL).toBe(MAX_VISUAL_DEPTH - 1);
    expect(MAX_VISUAL_DEPTH).toBe(3);
  });
});
