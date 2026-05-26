import Link from "next/link";
import { notFound } from "next/navigation";
import { formatDate } from "@/lib/utils";
import styles from "../../wiki.module.css";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";

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
// Kept for Table of Contents rendering
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
            <div className={styles.articleBody}>
              <ReactMarkdown 
                remarkPlugins={[remarkGfm]}
                components={{
                  h2: ({ children }) => {
                    const text = String(children);
                    const id = text.toLowerCase().replace(/[^\w\s-]/g, "").replace(/\s+/g, "-");
                    return <h2 id={id}>{children}</h2>;
                  },
                  h3: ({ children }) => {
                    const text = String(children);
                    const id = text.toLowerCase().replace(/[^\w\s-]/g, "").replace(/\s+/g, "-");
                    return <h3 id={id}>{children}</h3>;
                  }
                }}
              >
                {article.content}
              </ReactMarkdown>
            </div>
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
