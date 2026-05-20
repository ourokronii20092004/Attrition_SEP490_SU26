import Link from 'next/link';
import Breadcrumb from '@/components/Breadcrumb';
import { api } from '@/lib/api';

async function getForumCategories() {
  try {
    const res = await api.get('/api/forum/categories');
    return Array.isArray(res) ? res : (res.data || []);
  } catch {
    return [];
  }
}

async function getRecentThreads() {
  try {
    const res = await api.get('/api/forum/threads?pageSize=10');
    return res.items || [];
  } catch {
    return [];
  }
}

export default async function ForumPage() {
  const categories = await getForumCategories();
  const recentThreads = await getRecentThreads();

  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Forum' },
      ]} />

      <div className="section-header mb-xl">
        <h1>💬 Community Forum</h1>
        <Link href="/forum/new" className="btn btn-primary btn-sm">
          ✏️ New Thread
        </Link>
      </div>

      {/* Categories */}
      {categories.length > 0 && (
        <section className="mb-2xl">
          <h2 className="mb-lg">Categories</h2>
          <div className="category-grid">
            {categories.map((cat: any) => (
              <Link key={cat.id} href={`/forum/${cat.slug}`} className="category-card">
                <h3>{cat.name}</h3>
                <p>{cat.description || 'Discuss topics in this category'}</p>
                <div className="category-count">{cat.threadCount || 0} threads</div>
              </Link>
            ))}
          </div>
        </section>
      )}

      {/* Recent Threads */}
      {recentThreads.length > 0 && (
        <section>
          <h2 className="mb-lg">Recent Threads</h2>
          <div className="thread-list">
            {recentThreads.map((thread: any) => (
              <div key={thread.id} className="thread-item">
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-sm)', marginBottom: 'var(--space-xs)' }}>
                    {thread.isPinned && <span className="badge badge-pinned">📌 Pinned</span>}
                    {thread.isLocked && <span className="badge badge-locked">🔒 Locked</span>}
                  </div>
                  <Link href={`/forum/${thread.categorySlug || 'general'}/${thread.id}`}>
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
        </section>
      )}

      {categories.length === 0 && recentThreads.length === 0 && (
        <div className="empty-state">
          <div className="empty-state-icon">💬</div>
          <h3>Forum is empty</h3>
          <p>Be the first to start a discussion!</p>
        </div>
      )}
    </div>
  );
}