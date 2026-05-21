import Link from "next/link";
import { api } from "@/lib/api";
import styles from "./wiki.module.css";

export const dynamic = "force-dynamic";

interface WikiCategory {
  id: number;
  name: string;
  slug: string;
  description: string;
  iconUrl: string | null;
  articleCount?: number;
}

async function getCategories(): Promise<WikiCategory[]> {
  try {
    const res = await fetch(
      `${process.env.API_URL || "http://localhost:5000"}/api/wiki/categories`,
      { next: { revalidate: 1800 } } // Cache 30 min
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
  title: "Wiki",
  description: "The Attrition knowledge base — items, enemies, skills, lore, and game mechanics.",
};

export default async function WikiPage() {
  const categories = await getCategories();

  const categoryIcons: Record<string, string> = {
    "game-mechanics": "⚙",
    characters: "👤",
    items: "🗡",
    enemies: "👹",
    lore: "📖",
  };

  return (
    <div className="page">
      <div className="container">
        <div className="page-header">
          <div className="breadcrumb">
            <Link href="/">Home</Link>
            <span className="breadcrumb-separator">›</span>
            <span className="breadcrumb-current">Wiki</span>
          </div>
          <h1>Wiki</h1>
          <p>
            Explore the knowledge base for Attrition — everything you need to know about the game.
          </p>
        </div>

        {categories.length > 0 ? (
          <div className={styles.categoryGrid}>
            {categories.map((cat) => (
              <Link
                key={cat.id}
                href={`/wiki/${cat.slug}`}
                className={styles.categoryCard}
              >
                <span className={styles.categoryIcon}>
                  {categoryIcons[cat.slug] || "📄"}
                </span>
                <div className={styles.categoryInfo}>
                  <h3 className={styles.categoryName}>{cat.name}</h3>
                  <p className={styles.categoryDescription}>
                    {cat.description}
                  </p>
                </div>
                <span className={styles.categoryArrow}>→</span>
              </Link>
            ))}
          </div>
        ) : (
          <div className="empty-state">
            <span className="empty-state-icon">📖</span>
            <h3>No categories yet</h3>
            <p>Wiki categories will appear here once they are created.</p>
          </div>
        )}
      </div>
    </div>
  );
}
