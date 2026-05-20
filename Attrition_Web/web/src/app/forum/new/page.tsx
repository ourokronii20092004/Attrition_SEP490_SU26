'use client';
import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import Breadcrumb from '@/components/Breadcrumb';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useToast } from '@/contexts/ToastContext';

export default function NewThreadPage() {
  const { user, loading } = useAuth();
  const { showToast } = useToast();
  const router = useRouter();

  const [categories, setCategories] = useState<any[]>([]);
  const [categoryId, setCategoryId] = useState('');
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!loading && !user) {
      router.push('/auth/login');
    }
  }, [user, loading, router]);

  useEffect(() => {
    const fetchCategories = async () => {
      const res = await api.get('/api/forum/categories');
      const cats = Array.isArray(res) ? res : (res.data || []);
      setCategories(cats);
    };
    fetchCategories();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!categoryId || !title.trim() || !content.trim()) {
      showToast('Please fill in all fields', 'error');
      return;
    }

    setIsSubmitting(true);
    const res = await api.post('/api/forum/threads', {
      categoryId: parseInt(categoryId),
      title,
      content,
    });
    setIsSubmitting(false);

    if (res.success) {
      showToast('Thread created!', 'success');
      const catSlug = categories.find(c => c.id.toString() === categoryId)?.slug || 'general';
      router.push(`/forum/${catSlug}/${res.data?.id || res.data}`);
    } else {
      showToast(res.error || 'Failed to create thread', 'error');
    }
  };

  if (loading || !user) return null;

  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Forum', href: '/forum' },
        { label: 'New Thread' },
      ]} />

      <div className="glass-card-static" style={{ maxWidth: '700px', margin: '0 auto' }}>
        <h1 className="mb-xl">📝 Create New Thread</h1>

        <form className="auth-form" onSubmit={handleSubmit}>
          <div className="input-group">
            <label>Category</label>
            <select
              className="input"
              value={categoryId}
              onChange={(e) => setCategoryId(e.target.value)}
              required
            >
              <option value="" disabled>Select a category...</option>
              {categories.map(cat => (
                <option key={cat.id} value={cat.id}>{cat.name}</option>
              ))}
            </select>
          </div>

          <div className="input-group">
            <label>Title</label>
            <input
              className="input"
              type="text"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              placeholder="Thread title..."
              required
            />
          </div>

          <div className="input-group">
            <label>Content</label>
            <textarea
              className="input"
              value={content}
              onChange={(e) => setContent(e.target.value)}
              placeholder="What's on your mind?"
              rows={8}
              required
            />
          </div>

          <div style={{ display: 'flex', gap: 'var(--space-md)' }}>
            <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
              {isSubmitting ? 'Creating...' : '🔥 Create Thread'}
            </button>
            <button type="button" className="btn btn-ghost" onClick={() => router.back()}>
              Cancel
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}