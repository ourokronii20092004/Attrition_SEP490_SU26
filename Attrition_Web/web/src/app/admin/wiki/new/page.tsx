'use client';
import { useState, useEffect } from 'react';
import GlassCard from '@/components/GlassCard';
import Breadcrumb from '@/components/Breadcrumb';
import RichEditor from '@/components/RichEditor';
import Input from '@/components/Input';
import Button from '@/components/Button';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useRouter } from 'next/navigation';
import { useToast } from '@/contexts/ToastContext';

export default function NewWikiArticle() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const { showToast } = useToast();

  const [categories, setCategories] = useState<any[]>([]);
  const [categoryId, setCategoryId] = useState('');
  const [title, setTitle] = useState('');
  const [content, setContent] = useState('');
  const [status, setStatus] = useState('Published');
  const [isSubmitting, setIsSubmitting] = useState(false);

  useEffect(() => {
    if (!loading && user?.role !== 'Admin') {
      router.push('/');
    }
  }, [user, loading, router]);

  useEffect(() => {
    const fetchCategories = async () => {
      const res = await api.get('/api/wiki/categories');
      if (res && res.length > 0) {
        setCategories(res);
      }
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
    const res = await api.post('/api/wiki/articles', {
      title,
      categoryId: parseInt(categoryId),
      content,
      status
    });
    setIsSubmitting(false);

    if (res.success) {
      showToast('Article created successfully', 'success');
      const catSlug = categories.find(c => c.id.toString() === categoryId)?.slug || 'general';
      router.push(`/wiki/${catSlug}/${res.data}`);
    } else {
      showToast(res.error || 'Failed to create article', 'error');
    }
  };

  if (loading || user?.role !== 'Admin') return null;

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <Breadcrumb items={[
        { label: 'Admin', href: '/admin' },
        { label: 'New Wiki Article' }
      ]} />

      <GlassCard style={{ maxWidth: '800px', margin: '0 auto' }}>
        <h1 style={{ marginBottom: 'var(--space-xl)' }}>Create Wiki Article</h1>

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
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

          <Input 
            label="Title" 
            value={title} 
            onChange={(e) => setTitle(e.target.value)} 
            placeholder="e.g. Flame Sword of the Abyss"
            required 
          />

          <div className="input-group">
            <label>Content</label>
            <RichEditor value={content} onChange={setContent} height={500} />
          </div>

          <div className="input-group">
            <label>Status</label>
            <select className="input" value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="Draft">Draft</option>
              <option value="Published">Published</option>
            </select>
          </div>

          <div style={{ display: 'flex', gap: 'var(--space-md)', marginTop: 'var(--space-md)' }}>
            <Button type="submit" disabled={isSubmitting || !categoryId || !title.trim() || !content.trim()}>
              {isSubmitting ? 'Creating...' : 'Create Article'}
            </Button>
            <Button type="button" variant="ghost" onClick={() => router.back()}>Cancel</Button>
          </div>
        </form>
      </GlassCard>
    </div>
  );
}