'use client';
import { useState, useEffect } from 'react';
import GlassCard from '@/components/GlassCard';
import Breadcrumb from '@/components/Breadcrumb';
import MarkdownRenderer from '@/components/MarkdownRenderer';
import RichEditor from '@/components/RichEditor';
import Pagination from '@/components/Pagination';
import Avatar from '@/components/Avatar';
import Badge from '@/components/Badge';
import Button from '@/components/Button';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { FiThumbsUp, FiThumbsDown, FiLock, FiTrash2, FiEdit2 } from 'react-icons/fi';
import { useToast } from '@/contexts/ToastContext';
import { useRouter } from 'next/navigation';

export default function ForumThread({ params }: { params: { category: string, threadId: string } }) {
  const [thread, setThread] = useState<any>(null);
  const [posts, setPosts] = useState<any[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(true);
  const [replyContent, setReplyContent] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  
  const { user } = useAuth();
  const { showToast } = useToast();
  const router = useRouter();

  const fetchThreadAndPosts = async () => {
    setLoading(true);
    try {
      const threadRes = await api.get(`/api/forum/threads/${params.threadId}`);
      if (threadRes.success) setThread(threadRes.data);

      const postsRes = await api.get(`/api/forum/threads/${params.threadId}/posts?page=${page}`);
      if (postsRes.items) {
        setPosts(postsRes.items);
        setTotal(postsRes.totalCount);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchThreadAndPosts();
  }, [params.threadId, page]);

  const handleReply = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!replyContent.trim()) return;

    setIsSubmitting(true);
    const res = await api.post(`/api/forum/threads/${params.threadId}/posts`, { content: replyContent });
    setIsSubmitting(false);

    if (res.success) {
      setReplyContent('');
      showToast('Reply posted successfully', 'success');
      // Go to last page to see new post
      if (page !== Math.ceil((total + 1) / 20)) {
        setPage(Math.ceil((total + 1) / 20));
      } else {
        fetchThreadAndPosts();
      }
    } else {
      showToast(res.error || 'Failed to post reply', 'error');
    }
  };

  const handleReaction = async (postId: string, type: 'like' | 'dislike') => {
    const res = await api.post(`/api/forum/posts/${postId}/react`, { reactionType: type });
    if (res.success) {
      fetchThreadAndPosts();
    } else {
      showToast(res.error || 'Failed to react', 'error');
    }
  };

  const handleDeletePost = async (postId: string) => {
    if (!confirm('Are you sure you want to delete this post?')) return;
    
    const res = await api.delete(`/api/forum/posts/${postId}`);
    if (res.success) {
      showToast('Post deleted', 'success');
      fetchThreadAndPosts();
    } else {
      showToast(res.error || 'Failed to delete post', 'error');
    }
  };

  const togglePin = async () => {
    const res = await api.put(`/api/forum/threads/${params.threadId}/pin`);
    if (res.success) {
      showToast('Thread pin status toggled', 'success');
      fetchThreadAndPosts();
    }
  };

  const toggleLock = async () => {
    const res = await api.put(`/api/forum/threads/${params.threadId}/lock`);
    if (res.success) {
      showToast('Thread lock status toggled', 'success');
      fetchThreadAndPosts();
    }
  };

  const deleteThread = async () => {
    if (!confirm('Are you sure you want to delete this entire thread?')) return;
    const res = await api.delete(`/api/forum/threads/${params.threadId}`);
    if (res.success) {
      showToast('Thread deleted', 'success');
      router.push(`/forum/${params.category}`);
    }
  };

  if (loading && !thread) return <div className="container" style={{ padding: 'var(--space-3xl)', textAlign: 'center' }}>Loading...</div>;
  if (!thread) return <div className="container" style={{ padding: 'var(--space-3xl)', textAlign: 'center' }}>Thread not found</div>;

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Forum', href: '/forum' },
        { label: params.category.charAt(0).toUpperCase() + params.category.slice(1).replace('-', ' '), href: `/forum/${params.category}` },
        { label: thread.title }
      ]} />

      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-xl)', flexWrap: 'wrap', gap: 'var(--space-md)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-md)' }}>
          <h1 style={{ margin: 0 }}>{thread.title}</h1>
          {thread.isPinned && <Badge variant="pinned">Pinned</Badge>}
          {thread.isLocked && <Badge variant="locked"><FiLock style={{ marginRight: '4px' }} /> Locked</Badge>}
        </div>

        {user?.role === 'Admin' && (
          <div style={{ display: 'flex', gap: 'var(--space-sm)' }}>
            <Button variant="secondary" size="sm" onClick={togglePin}>{thread.isPinned ? 'Unpin' : 'Pin'}</Button>
            <Button variant="secondary" size="sm" onClick={toggleLock}>{thread.isLocked ? 'Unlock' : 'Lock'}</Button>
            <Button variant="danger" size="sm" onClick={deleteThread}>Delete Thread</Button>
          </div>
        )}
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
        {posts.map((post, index) => (
          <GlassCard key={post.id} style={{ display: 'flex', gap: 'var(--space-lg)', padding: 'var(--space-md)', opacity: post.content === '[deleted]' ? 0.5 : 1 }}>
            <div style={{ width: '120px', display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center', flexShrink: 0 }}>
              <Avatar username={post.authorName} src={post.authorAvatar} size="lg" />
              <div style={{ marginTop: 'var(--space-sm)', fontWeight: 'bold', wordBreak: 'break-all' }}>{post.authorName}</div>
              <Badge variant={post.authorRole === 'Admin' ? 'admin' : 'user'}><span style={{ fontSize: '10px' }}>{post.authorRole}</span></Badge>
            </div>

            <div style={{ flex: 1, minWidth: 0, display: 'flex', flexDirection: 'column' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 'var(--space-md)', color: 'var(--text-muted)', fontSize: '12px', borderBottom: '1px solid var(--border)', paddingBottom: 'var(--space-xs)' }}>
                <span>{new Date(post.createdAt).toLocaleString()} {post.updatedAt && '(edited)'}</span>
                <span style={{ fontWeight: 'bold' }}>#{((page - 1) * 20) + index + 1}</span>
              </div>
              
              <div style={{ flex: 1 }}>
                <MarkdownRenderer content={post.content} />
              </div>

              <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 'var(--space-md)', paddingTop: 'var(--space-sm)' }}>
                <div style={{ display: 'flex', gap: 'var(--space-sm)' }}>
                  <button 
                    onClick={() => user ? handleReaction(post.id, 'like') : showToast('Login to react', 'info')}
                    className="btn btn-ghost btn-sm"
                    style={{ color: post.currentUserReaction === 'like' ? 'var(--accent)' : 'var(--text-secondary)' }}
                  >
                    <FiThumbsUp /> <span style={{ marginLeft: '4px' }}>{post.likeCount}</span>
                  </button>
                  <button 
                    onClick={() => user ? handleReaction(post.id, 'dislike') : showToast('Login to react', 'info')}
                    className="btn btn-ghost btn-sm"
                    style={{ color: post.currentUserReaction === 'dislike' ? 'var(--danger)' : 'var(--text-secondary)' }}
                  >
                    <FiThumbsDown /> <span style={{ marginLeft: '4px' }}>{post.dislikeCount}</span>
                  </button>
                </div>

                <div style={{ display: 'flex', gap: 'var(--space-sm)' }}>
                  {(user?.username === post.authorName || user?.role === 'Admin') && (
                    <>
                      {/* Editing could open a modal or inline editor. Omitting full edit flow for brevity, but here's the delete button */}
                      <button onClick={() => handleDeletePost(post.id)} className="btn btn-ghost btn-sm" style={{ color: 'var(--danger)' }}>
                        <FiTrash2 />
                      </button>
                    </>
                  )}
                </div>
              </div>
            </div>
          </GlassCard>
        ))}
      </div>

      <Pagination currentPage={page} totalPages={Math.ceil(total / 20)} onPageChange={setPage} />

      {user && !thread.isLocked && (
        <GlassCard style={{ marginTop: 'var(--space-2xl)' }}>
          <h3 style={{ marginBottom: 'var(--space-md)' }}>Post a Reply</h3>
          <form onSubmit={handleReply} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
            <RichEditor value={replyContent} onChange={setReplyContent} height={300} />
            <Button type="submit" disabled={isSubmitting || !replyContent.trim()} style={{ alignSelf: 'flex-start' }}>
              {isSubmitting ? 'Posting...' : 'Post Reply'}
            </Button>
          </form>
        </GlassCard>
      )}

      {user && thread.isLocked && (
        <div style={{ marginTop: 'var(--space-2xl)', textAlign: 'center', padding: 'var(--space-lg)', color: 'var(--danger)', background: 'rgba(220, 38, 38, 0.1)', borderRadius: 'var(--glass-radius-sm)' }}>
          <FiLock size={24} style={{ marginBottom: 'var(--space-sm)' }} />
          <p>This thread has been locked. You cannot post new replies.</p>
        </div>
      )}
    </div>
  );
}