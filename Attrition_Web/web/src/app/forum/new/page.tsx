'use client';
import { useState, useEffect, Suspense } from 'react';
import GlassCard from '@/components/GlassCard';
import Breadcrumb from '@/components/Breadcrumb';
import RichEditor from '@/components/RichEditor';
import Input from '@/components/Input';
import Button from '@/components/Button';
import { api } from '@/lib/api';
import { useAuth } from '@/contexts/AuthContext';
import { useRouter, useSearchParams } from 'next/navigation';
import { useToast } from '@/contexts/ToastContext';

export default function NewThreadPage() {
  return (
    <Suspense fallback={<div className="container" style={{ padding: 'var(--space-2xl) 0' }}>Loading...</div>}>
      <NewThread />
    </Suspense>
  );
}

function NewThread() {
  const { user, loading } = useAuth();
  const router = useRouter();
  const searchParams = useSearchParams();
  const { showToast } = useToast();
  
  const defaultCategory = searchParams.get('category');

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
      if (res && res.length > 0) {
        setCategories(res);
        if (defaultCategory) {
          const cat = res.find((c: any) => c.slug === defaultCategory);
          if (cat) setCategoryId(cat.id.toString());
        }
      }
    };
    fetchCategories();
  }, [defaultCategory]);

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
      content
    });
    setIsSubmitting(false);

    if (res.success) {
      showToast('Thread created successfully', 'success');
      const catSlug = categories.find(c => c.id.toString() === categoryId)?.slug || 'general';
      router.push(`/forum/${catSlug}/${res.data}`);
    } else {
      showToast(res.error || 'Failed to create thread', 'error');
    }
  };

  if (loading || !user) return null;

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Forum', href: '/forum' },
        { label: 'New Thread' }
      ]} />

      <GlassCard style={{ maxWidth: '800px', margin: '0 auto' }}>
        <h1 style={{ marginBottom: 'var(--space-xl)' }}>Create New Thread</h1>

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
            label="Thread Title" 
            value={title} 
            onChange={(e) => setTitle(e.target.value)} 
            placeholder="What do you want to discuss?"
            required 
          />

          <div className="input-group">
            <label>Post Content</label>
            <RichEditor value={content} onChange={setContent} height={400} />
          </div>

          <div style={{ display: 'flex', gap: 'var(--space-md)', marginTop: 'var(--space-md)' }}>
            <Button type="submit" disabled={isSubmitting || !categoryId || !title.trim() || !content.trim()}>
              {isSubmitting ? 'Creating...' : 'Create Thread'}
            </Button>
            <Button type="button" variant="ghost" onClick={() => router.back()}>Cancel</Button>
          </div>
        </form>
      </GlassCard>
    </div>
  );
}