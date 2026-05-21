import Link from "next/link";
import styles from "./forum.module.css";
import { NewThreadButton } from "./NewThreadButton";

export const dynamic = "force-dynamic";

interface ForumCategory {
  id: number;
  name: string;
  slug: string;
  description: string;
  threadCount?: number;
  lastActivity?: string;
}

async function getCategories(): Promise<ForumCategory[]> {
  try {
    const res = await fetch(
      `${process.env.API_URL || "http://localhost:5000"}/api/forum/categories`,
      { next: { revalidate: 300 } } // Cache 5 min
    );
    if (!res.ok) return [];
    const data = await res.json();
    // API may return raw array or { success, data } wrapper
    if (Array.isArray(data)) return data;
    return data.success ? data.data : [];
  } catch {
    return [];
  }
}

export const metadata = {
  title: "Forum",
  description: "Join the Attrition community — discuss strategies, report bugs, and share your journey.",
};

export default async function ForumPage() {
  const categories = await getCategories();

  return (
    <div className="page">
      <div className="container">
        <div className="page-header">
          <div className="breadcrumb">
            <Link href="/">Home</Link>
            <span className="breadcrumb-separator">›</span>
            <span className="breadcrumb-current">Forum</span>
          </div>
          <div className="flex items-center justify-between flex-wrap gap-4">
            <div>
              <h1>Forum</h1>
              <p>Discuss the game with the community.</p>
            </div>
            <NewThreadButton />
          </div>
        </div>

        {categories.length > 0 ? (
          <div className={styles.categoryList}>
            {categories.map((cat) => (
              <Link
                key={cat.id}
                href={`/forum/${cat.slug}`}
                className={styles.categoryRow}
              >
                <div className={styles.categoryInfo}>
                  <h3 className={styles.categoryName}>{cat.name}</h3>
                  <p className={styles.categoryDescription}>
                    {cat.description}
                  </p>
                </div>
                <div className={styles.categoryStats}>
                  <span className={styles.statValue}>
                    {cat.threadCount ?? 0}
                  </span>
                  <span className={styles.statLabel}>threads</span>
                </div>
                <span className={styles.categoryArrow}>→</span>
              </Link>
            ))}
          </div>
        ) : (
          <div className="empty-state">
            <span className="empty-state-icon">💬</span>
            <h3>No categories yet</h3>
            <p>Forum categories will appear here once they are created by an admin.</p>
          </div>
        )}
      </div>
    </div>
  );
}

