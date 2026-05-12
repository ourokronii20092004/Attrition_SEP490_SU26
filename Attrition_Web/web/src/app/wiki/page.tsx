import GlassCard from '@/components/GlassCard';
import Breadcrumb from '@/components/Breadcrumb';
import { api } from '@/lib/api';
import Link from 'next/link';

async function getCategories() {
  try {
    const res = await api.get('/api/wiki/categories');
    return res || [];
  } catch {
    return [];
  }
}

export default async function WikiIndex() {
  const categories = await getCategories();

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <Breadcrumb items={[{ label: 'Home', href: '/' }, { label: 'Wiki' }]} />
      
      <div style={{ marginBottom: 'var(--space-2xl)' }}>
        <h1 style={{ marginBottom: 'var(--space-md)' }}>Attrition Wiki</h1>
        <p style={{ color: 'var(--text-secondary)', fontSize: '1.1rem', maxWidth: '800px', lineHeight: 1.6 }}>
          Welcome to the official community wiki for Attrition. Here you can find detailed information about weapons, enemies, biomes, and game mechanics. Sign in to suggest edits and help grow the knowledge base.
        </p>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 'var(--space-lg)' }}>
        {categories.map((cat: any) => (
          <Link href={`/wiki/${cat.slug}`} key={cat.id} style={{ textDecoration: 'none' }}>
            <GlassCard style={{ height: '100%', display: 'flex', flexDirection: 'column', transition: 'all var(--transition-base)' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-md)', marginBottom: 'var(--space-sm)' }}>
                {cat.iconUrl && <span style={{ fontSize: '24px' }}>{cat.iconUrl}</span>}
                <h2 style={{ margin: 0, fontSize: '1.25rem', color: 'var(--text-primary)' }}>{cat.name}</h2>
              </div>
              <p style={{ color: 'var(--text-secondary)', flexGrow: 1, marginBottom: 'var(--space-md)' }}>
                {cat.description}
              </p>
              <div style={{ fontSize: '14px', color: 'var(--text-muted)' }}>
                {cat.articleCount} {cat.articleCount === 1 ? 'article' : 'articles'}
              </div>
            </GlassCard>
          </Link>
        ))}
      </div>
    </div>
  );
}