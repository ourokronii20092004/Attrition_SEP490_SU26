import Link from "next/link";
import { Fragment } from "react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { visit } from "unist-util-visit";
import type { Root, Text, PhrasingContent } from "mdast";

// Matches @username tokens (letters, digits, underscore — matching the backend username rule).
const MENTION = /(@[a-zA-Z0-9_]+)/g;

/**
 * Renders forum/post text with @mentions turned into links to the mentioned user's profile.
 * Preserves the existing whitespace-pre-wrap rendering. Content is already server-sanitized,
 * so we render it as plain text + mention links (no raw HTML injection).
 */
export function PostContent({ content, className }: { content: string; className?: string }) {
  return (
    <div className={className ?? "whitespace-pre-wrap text-sm leading-relaxed text-fg"}>
      {content.split(MENTION).map((part, i) => {
        if (i % 2 === 1) {
          const username = part.slice(1); // drop the leading @
          return (
            <Link
              key={i}
              href={`/u/${encodeURIComponent(username)}`}
              className="font-medium text-accent hover:underline"
            >
              {part}
            </Link>
          );
        }
        return <Fragment key={i}>{part}</Fragment>;
      })}
    </div>
  );
}

/**
 * Remark transformer that splits @username tokens inside text nodes into mdast `link` nodes
 * pointing at the user's profile, so @mentions stay clickable when rendering markdown.
 * Skips text already inside links/code to avoid mangling URLs or code spans.
 */
function remarkMentions() {
  return (tree: Root) => {
    visit(tree, "text", (node: Text, index, parent) => {
      if (index == null || !parent) return;
      // Skip text already inside a link so we don't mangle URLs. (inlineCode holds a raw
      // string, not text children, so it never reaches this text-node visitor.)
      if (parent.type === "link" || parent.type === "linkReference") return;
      const value = node.value;
      if (!value.includes("@")) return;
      MENTION.lastIndex = 0;
      const parts = value.split(MENTION);
      if (parts.length === 1) return;
      const replacement: PhrasingContent[] = [];
      for (let i = 0; i < parts.length; i++) {
        const part = parts[i];
        if (!part) continue;
        if (i % 2 === 1) {
          const username = part.slice(1);
          replacement.push({
            type: "link",
            url: `/u/${encodeURIComponent(username)}`,
            children: [{ type: "text", value: part }],
          });
        } else {
          replacement.push({ type: "text", value: part });
        }
      }
      parent.children.splice(index, 1, ...replacement);
      return index + replacement.length;
    });
  };
}

/**
 * Renders forum post content as Markdown (GFM) with @mentions preserved as profile links.
 * Used for the original post + create-thread preview; replies keep the plain PostContent.
 */
export function MarkdownContent({ content, className }: { content: string; className?: string }) {
  return (
    <div className={className ?? "prose-content"}>
      <ReactMarkdown
        remarkPlugins={[remarkGfm, remarkMentions]}
        components={{
          a: ({ href, children, ...props }) => {
            const url = href ?? "";
            if (url.startsWith("/u/")) {
              return (
                <Link href={url} className="font-medium text-accent hover:underline">
                  {children}
                </Link>
              );
            }
            return (
              <a href={url} target="_blank" rel="noopener noreferrer" {...props}>
                {children}
              </a>
            );
          },
        }}
      >
        {content}
      </ReactMarkdown>
    </div>
  );
}
