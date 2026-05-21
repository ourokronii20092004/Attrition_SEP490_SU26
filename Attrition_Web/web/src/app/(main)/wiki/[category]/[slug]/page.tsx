import Link from "next/link";
import { notFound } from "next/navigation";
import { formatDate } from "@/lib/utils";
import styles from "../../wiki.module.css";

interface Article {
  id: string;
  title: string;
  slug: string;
  content: string;
  categorySlug: string;
  authorName: string;
  lastEditorName: string | null;
  status: string;
  createdAt: string;
  updatedAt: string;
}

async function getArticle(slug: string): Promise<Article | null> {
  try {
    const res = await fetch(
      `${process.env.API_URL || "http://localhost:5000"}/api/wiki/articles/${encodeURIComponent(slug)}`,
      { next: { revalidate: 600 } }
    );
    if (!res.ok) return null;
    const data = await res.json();
    if (data.success && data.data) return data.data;
    // If not wrapped, the data might be the article directly
    if (data.id && data.title) return data;
    return null;
  } catch {
    return null;
  }
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ category: string; slug: string }>;
}) {
  const { slug } = await params;
  const article = await getArticle(slug);
  if (!article) return { title: "Article Not Found" };
  return {
    title: article.title,
    description: article.content.slice(0, 160).replace(/[#*_\[\]]/g, ""),
  };
}

// Simple markdown-to-HTML for headings extraction
function extractHeadings(content: string): { id: string; text: string; level: number }[] {
  const headingRegex = /^(#{2,3})\s+(.+)$/gm;
  const headings: { id: string; text: string; level: number }[] = [];
  let match;
  while ((match = headingRegex.exec(content)) !== null) {
    const text = match[2].trim();
    const id = text
      .toLowerCase()
      .replace(/[^\w\s-]/g, "")
      .replace(/\s+/g, "-");
    headings.push({ id, text, level: match[1].length });
  }
  return headings;
}

// Simple markdown renderer (basic subset)
function renderMarkdown(content: string): string {
  let html = content
    // Headings
    .replace(/^### (.+)$/gm, '<h3 id="$1">$1</h3>')
    .replace(/^## (.+)$/gm, '<h2 id="$1">$1</h2>')
    // Bold / Italic
    .replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>")
    .replace(/\*(.+?)\*/g, "<em>$1</em>")
    // Code
    .replace(/`(.+?)`/g, "<code>$1</code>")
    // Links
    .replace(/\[(.+?)\]\((.+?)\)/g, '<a href="$2">$1</a>')
    // Line breaks into paragraphs
    .replace(/\n\n/g, "</p><p>")
    // Lists
    .replace(/^- (.+)$/gm, "<li>$1</li>");

  // Fix heading IDs
  html = html.replace(/<h([23]) id="(.+?)">/g, (_, level, text) => {
    const id = text
      .toLowerCase()
      .replace(/[^\w\s-]/g, "")
      .replace(/\s+/g, "-");
    return `<h${level} id="${id}">`;
  });

  // Wrap in paragraphs
  html = `<p>${html}</p>`;
  // Clean up empty paragraphs
  html = html.replace(/<p>\s*<\/p>/g, "");
  html = html.replace(/<p>\s*<h/g, "<h");
  html = html.replace(/<\/h([23])>\s*<\/p>/g, "</h$1>");
  // Wrap lists
  html = html.replace(/(<li>[\s\S]*?<\/li>)/g, "<ul>$1</ul>");

  return html;
}

export default async function ArticlePage({
  params,
}: {
  params: Promise<{ category: string; slug: string }>;
}) {
  const { category, slug } = await params;
  const article = await getArticle(slug);
  if (!article) notFound();

  const categoryName = (article.categorySlug || category).replace(/-/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());
  const headings = extractHeadings(article.content);
  const html = renderMarkdown(article.content);

  return (
    <div className="page">
      <div className="container">
        <div className="breadcrumb">
          <Link href="/">Home</Link>
          <span className="breadcrumb-separator">›</span>
          <Link href="/wiki">Wiki</Link>
          <span className="breadcrumb-separator">›</span>
          <Link href={`/wiki/${article.categorySlug || category}`}>{categoryName}</Link>
          <span className="breadcrumb-separator">›</span>
          <span className="breadcrumb-current">{article.title}</span>
        </div>

        <div className={styles.articleLayout}>
          {/* Article content */}
          <article className={styles.articleContent}>
            <h1>{article.title}</h1>
            <div className={styles.articleMetaBar}>
              <span className="badge badge-accent">{categoryName}</span>
              <span>By {article.authorName}</span>
              <span>Updated {formatDate(article.updatedAt)}</span>
            </div>
            <div
              className={styles.articleBody}
              dangerouslySetInnerHTML={{ __html: html }}
            />
          </article>

          {/* TOC Sidebar */}
          {headings.length > 0 && (
            <aside className={styles.tocSidebar}>
              <div className={styles.tocTitle}>On this page</div>
              <nav className={styles.tocList}>
                {headings.map((h) => (
                  <a
                    key={h.id}
                    href={`#${h.id}`}
                    className={`${styles.tocLink} ${h.level === 3 ? styles.tocLinkH3 : ""}`}
                  >
                    {h.text}
                  </a>
                ))}
              </nav>
            </aside>
          )}
        </div>
      </div>
    </div>
  );
}
