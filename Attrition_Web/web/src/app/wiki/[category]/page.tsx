import Link from 'next/link';
import Breadcrumb from '@/components/Breadcrumb';
import { api } from '@/lib/api';

async function getCategoryArticles(categorySlug: string) {
  try {
    const res = await api.get(`/api/wiki/articles?category=${categorySlug}&pageSize=50`);
    return { items: res.items || [], totalCount: res.totalCount || 0 };
  } catch {
    return { items: [], totalCount: 0 };
  }
}

export default async function WikiCategoryPage({ params }: { params: { category: string } }) {
  const { items: articles } = await getCategoryArticles(params.category);
  const categoryName = params.category.replace(/-/g, ' ').replace(/\b\w/g, l => l.toUpperCase());

  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Wiki', href: '/wiki' },
        { label: categoryName },
      ]} />

      <div className="section-header mb-xl">
        <h1>{categoryName}</h1>
        <span className="badge badge-ember">{articles.length} articles</span>
      </div>

      {articles.length > 0 ? (
        <div className="grid-auto">
          {articles.map((article: any) => (
            <Link
              key={article.id}
              href={`/wiki/${params.category}/${article.slug}`}
              className="category-card"
            >
              <h3>{article.title}</h3>
              <p className="text-muted" style={{ fontSize: '13px', marginTop: 'var(--space-xs)' }}>
                By {article.authorName || 'Unknown'} •{' '}
                {new Date(article.updatedAt).toLocaleDateString()}
              </p>
            </Link>
          ))}
        </div>
      ) : (
        <div className="empty-state">
          <div className="empty-state-icon">📂</div>
          <h3>No articles yet</h3>
          <p>This category doesn&apos;t have any articles yet.</p>
        </div>
      )}
    </div>
  );
}