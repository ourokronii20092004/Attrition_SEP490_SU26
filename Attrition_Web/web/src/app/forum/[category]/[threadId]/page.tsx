'use client';
import { useState, useEffect } from 'react';
import Breadcrumb from '@/components/Breadcrumb';
import Avatar from '@/components/Avatar';
import Badge from '@/components/Badge';
import MarkdownRenderer from '@/components/MarkdownRenderer';
import Pagination from '@/components/Pagination';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';

export default function ThreadPage({ params }: { params: { category: string; threadId: string } }) {
  const { user } = useAuth();
  const { showToast } = useToast();
  const [thread, setThread] = useState<any>(null);
  const [posts, setPosts] = useState<any[]>([]);
  const [replyContent, setReplyContent] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);

  const categoryName = params.category.replace(/-/g, ' ').replace(/\b\w/g, l => l.toUpperCase());

  useEffect(() => {
    const fetchThread = async () => {
      const res = await api.get(`/api/forum/threads/${params.threadId}`);
      if (res.success) {
        setThread(res.data);
      }
    };
    fetchThread();
  }, [params.threadId]);

  useEffect(() => {
    const fetchPosts = async () => {
      setLoading(true);
      const res = await api.get(`/api/forum/threads/${params.threadId}/posts?page=${page}&pageSize=10`);
      if (res.items) {
        setPosts(res.items);
        setTotalPages(Math.ceil((res.totalCount || 0) / 10));
      }
      setLoading(false);
    };
    fetchPosts();
  }, [params.threadId, page]);

  const handleReply = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!replyContent.trim()) return;

    setIsSubmitting(true);
    const res = await api.post(`/api/forum/threads/${params.threadId}/posts`, {
      content: replyContent,
    });
    setIsSubmitting(false);

    if (res.success) {
      showToast('Reply posted!', 'success');
      setReplyContent('');
      // Refresh posts
      const postsRes = await api.get(`/api/forum/threads/${params.threadId}/posts?page=${page}&pageSize=10`);
      if (postsRes.items) {
        setPosts(postsRes.items);
        setTotalPages(Math.ceil((postsRes.totalCount || 0) / 10));
      }
    } else {
      showToast(res.error || 'Failed to post reply', 'error');
    }
  };

  if (!thread && loading) {
    return (
      <div className="loading-screen">
        <div className="spinner spinner-lg" />
      </div>
    );
  }

  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Forum', href: '/forum' },
        { label: categoryName, href: `/forum/${params.category}` },
        { label: thread?.title || 'Thread' },
      ]} />

      {thread && (
        <div className="mb-xl">
          <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-sm)', marginBottom: 'var(--space-sm)' }}>
            {thread.isPinned && <Badge variant="pinned">📌 Pinned</Badge>}
            {thread.isLocked && <Badge variant="locked">🔒 Locked</Badge>}
          </div>
          <h1 className="mb-sm">{thread.title}</h1>
          <p className="text-muted" style={{ fontSize: '14px' }}>
            Started by {thread.authorName} • {new Date(thread.createdAt).toLocaleDateString()}
          </p>
        </div>
      )}

      {/* Thread original post */}
      {thread?.content && (
        <div className="post-card mb-lg">
          <div className="post-card-header">
            <Avatar src={thread.authorAvatarUrl} alt={thread.authorName} />
            <div>
              <strong>{thread.authorName}</strong>
              <div className="text-muted" style={{ fontSize: '13px' }}>
                {new Date(thread.createdAt).toLocaleString()}
              </div>
            </div>
          </div>
          <MarkdownRenderer content={thread.content} />
        </div>
      )}

      {/* Replies */}
      {posts.length > 0 && (
        <section className="mb-xl">
          <h3 className="mb-md">Replies ({thread?.replyCount || posts.length})</h3>
          {posts.map((post: any) => (
            <div key={post.id} className="post-card">
              <div className="post-card-header">
                <Avatar src={post.authorAvatarUrl} alt={post.authorName} />
                <div>
                  <strong>{post.authorName}</strong>
                  {post.authorRole === 'Admin' && <Badge variant="admin" className="ml-sm">Admin</Badge>}
                  <div className="text-muted" style={{ fontSize: '13px' }}>
                    {new Date(post.createdAt).toLocaleString()}
                  </div>
                </div>
              </div>
              {post.isRemoved ? (
                <p className="text-muted" style={{ fontStyle: 'italic' }}>
                  [Removed by moderator]{post.removeReason ? ` — ${post.removeReason}` : ''}
                </p>
              ) : (
                <MarkdownRenderer content={post.content} />
              )}
            </div>
          ))}
          <Pagination currentPage={page} totalPages={totalPages} onPageChange={setPage} />
        </section>
      )}

      {/* Reply form */}
      {user && !thread?.isLocked && (
        <div className="glass-card-static">
          <h3 className="mb-md">Post a Reply</h3>
          <form onSubmit={handleReply}>
            <textarea
              className="input mb-md"
              value={replyContent}
              onChange={(e) => setReplyContent(e.target.value)}
              placeholder="Write your reply..."
              rows={4}
              required
            />
            <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
              {isSubmitting ? 'Posting...' : '💬 Post Reply'}
            </button>
          </form>
        </div>
      )}

      {thread?.isLocked && (
        <div className="glass-card-static text-center">
          <p className="text-muted">🔒 This thread is locked. No new replies can be posted.</p>
        </div>
      )}
    </div>
  );
}