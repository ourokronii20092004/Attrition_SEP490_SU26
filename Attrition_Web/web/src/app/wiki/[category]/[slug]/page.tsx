import Breadcrumb from '@/components/Breadcrumb';
import MarkdownRenderer from '@/components/MarkdownRenderer';
import { api } from '@/lib/api';
import { notFound } from 'next/navigation';

async function getArticle(slug: string) {
  try {
    const res = await api.get(`/api/wiki/articles/${slug}`);
    if (res.success) return res.data;
    return null;
  } catch {
    return null;
  }
}

export default async function WikiArticlePage({
  params,
}: {
  params: { category: string; slug: string };
}) {
  const article = await getArticle(params.slug);

  if (!article) {
    notFound();
  }

  const categoryName = params.category.replace(/-/g, ' ').replace(/\b\w/g, l => l.toUpperCase());

  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Wiki', href: '/wiki' },
        { label: categoryName, href: `/wiki/${params.category}` },
        { label: article.title },
      ]} />

      <article className="glass-card-static" style={{ maxWidth: '900px', margin: '0 auto', padding: 'var(--space-2xl)' }}>
        <header className="mb-xl">
          <h1 className="mb-sm">{article.title}</h1>
          <div className="text-muted" style={{ fontSize: '14px', display: 'flex', gap: 'var(--space-lg)', flexWrap: 'wrap' }}>
            <span>By {article.authorName || 'Unknown'}</span>
            <span>Updated {new Date(article.updatedAt).toLocaleDateString()}</span>
            {article.status === 'Draft' && <span className="badge badge-warning">Draft</span>}
          </div>
        </header>

        <MarkdownRenderer content={article.content} />

        {article.revisionHistory && article.revisionHistory.length > 0 && (
          <section className="mt-2xl" style={{ borderTop: '1px solid var(--border-dim)', paddingTop: 'var(--space-lg)' }}>
            <h3 className="mb-md">Revision History</h3>
            <div className="flex-col gap-sm">
              {article.revisionHistory.slice(0, 5).map((rev: any, i: number) => (
                <div key={i} className="text-muted" style={{ fontSize: '13px' }}>
                  <strong>{rev.editorName}</strong> — {rev.changeNote || 'No note'} ({new Date(rev.editedAt).toLocaleDateString()})
                </div>
              ))}
            </div>
          </section>
        )}
      </article>
    </div>
  );
}