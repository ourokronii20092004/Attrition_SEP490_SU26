import GlassCard from '@/components/GlassCard';
import Breadcrumb from '@/components/Breadcrumb';
import { api } from '@/lib/api';
import Link from 'next/link';

async function getCategories() {
  try {
    const res = await api.get('/api/forum/categories');
    return res || [];
  } catch {
    return [];
  }
}

export default async function ForumIndex() {
  const categories = await getCategories();

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <Breadcrumb items={[{ label: 'Home', href: '/' }, { label: 'Forum' }]} />
      
      <div style={{ marginBottom: 'var(--space-2xl)' }}>
        <h1 style={{ marginBottom: 'var(--space-md)' }}>Community Forum</h1>
        <p style={{ color: 'var(--text-secondary)', fontSize: '1.1rem', maxWidth: '800px', lineHeight: 1.6 }}>
          Join the conversation! Discuss builds, report bugs, share fan art, or just hang out with other players.
        </p>
      </div>

      <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
        {categories.map((cat: any) => (
          <GlassCard key={cat.id} style={{ transition: 'all var(--transition-base)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: 'var(--space-md)' }}>
              <div style={{ flex: 1, minWidth: '300px' }}>
                <Link href={`/forum/${cat.slug}`} style={{ textDecoration: 'none' }}>
                  <h2 style={{ margin: '0 0 var(--space-xs) 0', fontSize: '1.25rem', color: 'var(--accent)' }}>{cat.name}</h2>
                </Link>
                <p style={{ color: 'var(--text-secondary)', margin: 0 }}>{cat.description}</p>
              </div>
              <div style={{ display: 'flex', gap: 'var(--space-xl)', textAlign: 'right' }}>
                <div>
                  <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: 'var(--text-primary)' }}>{cat.threadCount}</div>
                  <div style={{ fontSize: '12px', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '1px' }}>Threads</div>
                </div>
                {cat.latestActivity && (
                  <div>
                    <div style={{ fontSize: '14px', color: 'var(--text-primary)', marginTop: '4px' }}>
                      {new Date(cat.latestActivity).toLocaleDateString()}
                    </div>
                    <div style={{ fontSize: '12px', color: 'var(--text-muted)' }}>Latest Activity</div>
                  </div>
                )}
              </div>
            </div>
          </GlassCard>
        ))}
      </div>
    </div>
  );
}