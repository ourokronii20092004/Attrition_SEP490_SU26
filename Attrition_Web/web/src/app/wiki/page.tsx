import Link from 'next/link';
import Breadcrumb from '@/components/Breadcrumb';
import { api } from '@/lib/api';

async function getCategories() {
  try {
    const res = await api.get('/api/wiki/categories');
    return Array.isArray(res) ? res : (res.data || []);
  } catch {
    return [];
  }
}

async function getRecentArticles() {
  try {
    const res = await api.get('/api/wiki/articles?pageSize=6');
    return res.items || [];
  } catch {
    return [];
  }
}

export default async function WikiPage() {
  const categories = await getCategories();
  const recentArticles = await getRecentArticles();

  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Wiki' },
      ]} />

      <div className="section-header mb-xl">
        <h1>📖 Attrition Wiki</h1>
        <Link href="/wiki/contribute" className="btn btn-secondary btn-sm">
          ✏️ Contribute
        </Link>
      </div>

      {/* Categories */}
      {categories.length > 0 && (
        <section className="mb-2xl">
          <h2 className="mb-lg">Categories</h2>
          <div className="category-grid">
            {categories.map((cat: any) => (
              <Link key={cat.id} href={`/wiki/${cat.slug}`} className="category-card">
                <h3>{cat.name}</h3>
                <p>{cat.description || 'Explore articles in this category'}</p>
                <div className="category-count">{cat.articleCount || 0} articles</div>
              </Link>
            ))}
          </div>
        </section>
      )}

      {/* Recent Articles */}
      {recentArticles.length > 0 && (
        <section>
          <h2 className="mb-lg">Recent Articles</h2>
          <div className="grid-auto">
            {recentArticles.map((article: any) => (
              <Link
                key={article.id}
                href={`/wiki/${article.categorySlug}/${article.slug}`}
                className="category-card"
              >
                <h3>{article.title}</h3>
                <p className="text-muted" style={{ fontSize: '13px', marginTop: 'var(--space-xs)' }}>
                  in {article.categoryName || article.categorySlug} •{' '}
                  {new Date(article.updatedAt).toLocaleDateString()}
                </p>
              </Link>
            ))}
          </div>
        </section>
      )}

      {categories.length === 0 && recentArticles.length === 0 && (
        <div className="empty-state">
          <div className="empty-state-icon">📖</div>
          <h3>The wiki is empty</h3>
          <p>No articles have been created yet. Be the first to contribute!</p>
        </div>
      )}
    </div>
  );
}