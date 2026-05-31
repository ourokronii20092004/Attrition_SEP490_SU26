import Link from "next/link";
import { Fragment } from "react";

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
