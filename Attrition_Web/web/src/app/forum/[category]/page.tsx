import Link from 'next/link';
import Breadcrumb from '@/components/Breadcrumb';
import { api } from '@/lib/api';

async function getCategoryThreads(categorySlug: string) {
  try {
    const res = await api.get(`/api/forum/threads?category=${categorySlug}&pageSize=20`);
    return { items: res.items || [], totalCount: res.totalCount || 0 };
  } catch {
    return { items: [], totalCount: 0 };
  }
}

export default async function ForumCategoryPage({ params }: { params: { category: string } }) {
  const { items: threads } = await getCategoryThreads(params.category);
  const categoryName = params.category.replace(/-/g, ' ').replace(/\b\w/g, l => l.toUpperCase());

  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Forum', href: '/forum' },
        { label: categoryName },
      ]} />

      <div className="section-header mb-xl">
        <h1>{categoryName}</h1>
        <Link href="/forum/new" className="btn btn-primary btn-sm">
          ✏️ New Thread
        </Link>
      </div>

      {threads.length > 0 ? (
        <div className="thread-list">
          {threads.map((thread: any) => (
            <div key={thread.id} className="thread-item">
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-sm)', marginBottom: 'var(--space-xs)' }}>
                  {thread.isPinned && <span className="badge badge-pinned">📌 Pinned</span>}
                  {thread.isLocked && <span className="badge badge-locked">🔒 Locked</span>}
                </div>
                <Link href={`/forum/${params.category}/${thread.id}`}>
                  <h4 style={{ margin: 0 }}>{thread.title}</h4>
                </Link>
                <p className="text-muted" style={{ fontSize: '13px', marginTop: 'var(--space-xs)' }}>
                  By {thread.authorName} • {new Date(thread.createdAt).toLocaleDateString()}
                </p>
              </div>
              <div className="thread-meta">
                <span>{thread.replyCount || 0} replies</span>
              </div>
            </div>
          ))}
        </div>
      ) : (
        <div className="empty-state">
          <div className="empty-state-icon">💬</div>
          <h3>No threads yet</h3>
          <p>Be the first to start a discussion in this category.</p>
        </div>
      )}
    </div>
  );
}