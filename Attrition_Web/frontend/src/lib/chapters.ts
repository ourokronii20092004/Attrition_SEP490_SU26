import fs from "node:fs";
import path from "node:path";

// Chapter loader — reads the 17 Eldravir manuscript chapters bundled under
// src/content/chapters/NN.md at request time (server components only). Parses the
// lightweight YAML frontmatter by hand (no gray-matter dep) and strips the
// Obsidian-isms (the H1 title line, [[wikilinks]]) so the prose renders clean.

export interface ChapterMeta {
  /** 1-based chapter number, also the route segment and filename stem. */
  n: number;
  title: string;
  act: number;
  pov: string;
  /** Resolved plain-text stratum (wikilink brackets stripped), if present. */
  stratum?: string;
  /** Approx reading time in minutes. */
  readingMinutes: number;
}

export interface Chapter extends ChapterMeta {
  /** Body markdown, frontmatter + leading H1 removed, wikilinks flattened. */
  content: string;
}

const CHAPTERS_DIR = path.join(process.cwd(), "src", "content", "chapters");
export const CHAPTER_COUNT = 17;

export const ACTS: { act: number; name: string; subtitle: string }[] = [
  { act: 1, name: "Act I — The Hire", subtitle: "Ren wakes; Iris strikes the deal; the descent begins." },
  { act: 2, name: "Act II — The Five", subtitle: "The four outer pillars, understood and freed — each giving a rule." },
  { act: 3, name: "Act III — The Throne", subtitle: "The core, the reveals, the confession, and the choice." },
];

// Drop a leading [[ ... ]] target down to its display text: [[A|B]] -> B, [[A]] -> A.
function stripWikilinks(s: string): string {
  return s.replace(/\[\[([^\]]+)\]\]/g, (_, inner: string) => {
    const parts = String(inner).split("|");
    return parts[parts.length - 1].trim();
  });
}

function parseFrontmatter(raw: string): { data: Record<string, string>; body: string } {
  // Normalize CRLF -> LF before anything else. The chapter files are authored on Windows (and
  // .gitattributes only forces LF for *.sh), so a \n-anchored frontmatter regex silently failed
  // on every chapter — leaking the whole YAML block (title/tags/pov/stratum) into the prose.
  const text = raw.replace(/\r\n/g, "\n").replace(/\r/g, "\n");
  const m = text.match(/^---\n([\s\S]*?)\n---\n?([\s\S]*)$/);
  if (!m) return { data: {}, body: text };
  const data: Record<string, string> = {};
  let lastKey: string | null = null;
  for (const line of m[1].split("\n")) {
    // YAML block-sequence item ("  - chapter") belonging to the previous key. Collect it so a
    // list-valued key can never fall through and be mistaken for body text.
    const item = line.match(/^\s*-\s+(.*)$/);
    if (item && lastKey) {
      const v = item[1].trim().replace(/^["']|["']$/g, "");
      data[lastKey] = data[lastKey] ? `${data[lastKey]}, ${v}` : v;
      continue;
    }
    const kv = line.match(/^([a-zA-Z0-9_]+):\s*(.*)$/);
    if (kv) {
      lastKey = kv[1];
      data[kv[1]] = kv[2].trim().replace(/^["']|["']$/g, "");
    }
  }
  return { data, body: m[2] };
}

function cleanBody(body: string): string {
  let out = body;
  // Belt-and-braces: if a file ever lacks the closing "---", strip a leading YAML-ish block so it
  // still can't render as prose.
  out = out.replace(/^---\n[\s\S]*?\n---\n?/, "");
  // Remove the first markdown H1 (the "# Ch01 · Arrival" title line) — the page renders its own title.
  out = out.replace(/^\s*#\s+.*\n/, "");
  // Flatten any wikilinks that appear in the prose to plain text.
  out = stripWikilinks(out);
  return out.trim();
}

function prettyTitle(rawTitle: string, n: number): string {
  // Frontmatter title is like "Ch01 · Arrival" — keep just the human part.
  const after = rawTitle.split("·").slice(1).join("·").trim();
  return after || rawTitle || `Chapter ${n}`;
}

function loadRaw(n: number): { data: Record<string, string>; body: string } | null {
  const file = path.join(CHAPTERS_DIR, `${String(n).padStart(2, "0")}.md`);
  if (!fs.existsSync(file)) return null;
  return parseFrontmatter(fs.readFileSync(file, "utf8"));
}

function toMeta(n: number, data: Record<string, string>, body: string): ChapterMeta {
  const words = body.trim().split(/\s+/).length;
  return {
    n,
    title: prettyTitle(data.title ?? "", n),
    act: Number(data.act) || 1,
    pov: data.pov ?? "Ren",
    stratum: data.stratum ? stripWikilinks(data.stratum) : undefined,
    readingMinutes: Math.max(1, Math.round(words / 220)),
  };
}

/** All chapter metadata in order, for the index/listing. */
export function getAllChapterMeta(): ChapterMeta[] {
  const out: ChapterMeta[] = [];
  for (let n = 1; n <= CHAPTER_COUNT; n++) {
    const raw = loadRaw(n);
    if (raw) out.push(toMeta(n, raw.data, raw.body));
  }
  return out;
}

/** A single chapter with its cleaned body, or null if out of range/missing. */
export function getChapter(n: number): Chapter | null {
  if (!Number.isInteger(n) || n < 1 || n > CHAPTER_COUNT) return null;
  const raw = loadRaw(n);
  if (!raw) return null;
  return { ...toMeta(n, raw.data, raw.body), content: cleanBody(raw.body) };
}
