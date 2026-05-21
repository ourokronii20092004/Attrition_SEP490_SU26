import Link from "next/link";
import styles from "../wiki.module.css";

interface WikiArticle {
  id: string;
  title: string;
  slug: string;
  categorySlug: string;
  summary: string | null;
  createdAt: string;
  updatedAt: string;
  authorName: string | null;
}

import { Pagination } from "@/components/Pagination";

async function getArticles(category: string, page: number): Promise<{ items: WikiArticle[], totalCount: number, pageSize: number }> {
  try {
    const res = await fetch(
      `${process.env.API_URL || "http://localhost:5000"}/api/wiki/articles?category=${encodeURIComponent(category)}&page=${page}&pageSize=20`,
      { next: { revalidate: 600 } }
    );
    if (!res.ok) return { items: [], totalCount: 0, pageSize: 20 };
    const data = await res.json();
    if (data.items && Array.isArray(data.items)) return data;
    if (Array.isArray(data)) return { items: data, totalCount: data.length, pageSize: 20 };
    if (data.success && data.data && data.data.items) return data.data;
    return { items: [], totalCount: 0, pageSize: 20 };
  } catch {
    return { items: [], totalCount: 0, pageSize: 20 };
  }
}

export default async function WikiCategoryPage({
  params,
  searchParams,
}: {
  params: Promise<{ category: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { category } = await params;
  const sp = await searchParams;
  const page = parseInt(sp.page as string) || 1;
  const { items: articles, totalCount, pageSize } = await getArticles(category, page);
  const categoryName = category.replace(/-/g, " ").replace(/\b\w/g, (c) => c.toUpperCase());

  return (
    <div className="page">
      <div className="container">
        <div className="page-header">
          <div className="breadcrumb">
            <Link href="/">Home</Link>
            <span className="breadcrumb-separator">›</span>
            <Link href="/wiki">Wiki</Link>
            <span className="breadcrumb-separator">›</span>
            <span className="breadcrumb-current">{categoryName}</span>
          </div>
          <h1>{categoryName}</h1>
          <p>{articles.length} {articles.length === 1 ? "article" : "articles"}</p>
        </div>

        {articles.length > 0 ? (
          <div className={styles.categoryGrid}>
            {articles.map((article) => (
              <Link
                key={article.id}
                href={`/wiki/${category}/${article.slug}`}
                className={styles.categoryCard}
              >
                <div className={styles.categoryInfo}>
                  <h3 className={styles.categoryName}>{article.title}</h3>
                  {article.summary && (
                    <p className={styles.categoryDescription}>
                      {article.summary.length > 120
                        ? article.summary.slice(0, 120) + "…"
                        : article.summary}
                    </p>
                  )}
                </div>
                <span className={styles.categoryArrow}>→</span>
              </Link>
            ))}
          </div>
        ) : (
          <div className="empty-state">
            <span className="empty-state-icon">📄</span>
            <h3>No articles yet</h3>
            <p>Articles in this category will appear here once they are created.</p>
          </div>
        )}

        <Pagination 
          currentPage={page} 
          totalCount={totalCount} 
          pageSize={pageSize} 
          baseUrl={`/wiki/${category}`} 
        />
      </div>
    </div>
  );
}
