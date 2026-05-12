import Link from 'next/link';
import GlassCard from '@/components/GlassCard';
import Button from '@/components/Button';
import { api } from '@/lib/api';

async function getWikiArticles() {
  try {
    const res = await api.get('/api/wiki/articles?pageSize=4');
    return res.items ? res.items : [];
  } catch {
    return [];
  }
}

async function getForumThreads() {
  try {
    const res = await api.get('/api/forum/threads?pageSize=5');
    return res.items ? res.items : [];
  } catch {
    return [];
  }
}

export default async function Home() {
  const wikiArticles = await getWikiArticles();
  const forumThreads = await getForumThreads();

  return (
    <div>
      {/* Hero Section */}
      <section style={{
        position: 'relative',
        padding: 'var(--space-3xl) 0',
        minHeight: '60vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'linear-gradient(135deg, var(--accent-secondary) 0%, var(--bg-primary) 100%)',
        overflow: 'hidden'
      }}>
        <div style={{
          position: 'absolute', top: 0, left: 0, right: 0, bottom: 0,
          backgroundImage: 'url(https://images.unsplash.com/photo-1550745165-9bc0b252726f?auto=format&fit=crop&q=80)',
          backgroundSize: 'cover', backgroundPosition: 'center', opacity: 0.15, mixBlendMode: 'overlay'
        }} />
        <div className="container" style={{ position: 'relative', zIndex: 1, textAlign: 'center' }}>
          <h1 style={{ fontSize: '4rem', fontWeight: 800, marginBottom: 'var(--space-md)', color: '#fff', textShadow: '0 4px 12px rgba(0,0,0,0.3)' }}>
            Attrition
          </h1>
          <p style={{ fontSize: '1.5rem', color: '#e2e8f0', marginBottom: 'var(--space-xl)', maxWidth: '600px', margin: '0 auto var(--space-xl) auto' }}>
            A 2D Roguelike with Built-in Multiplayer
          </p>
          <div style={{ display: 'flex', gap: 'var(--space-md)', justifyContent: 'center' }}>
            <Link href="/wiki">
              <Button size="lg">Explore Wiki</Button>
            </Link>
            <Link href="/forum">
              <Button variant="secondary" size="lg">Join Forum</Button>
            </Link>
          </div>
        </div>
      </section>

      <div className="container" style={{ marginTop: 'var(--space-2xl)' }}>
        {/* Features Grid */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: 'var(--space-lg)', marginBottom: 'var(--space-3xl)' }}>
          <GlassCard>
            <h3>🌍 Explore Biomes</h3>
            <p style={{ color: 'var(--text-secondary)' }}>Procedurally generated dungeons with unique enemies, traps, and secrets.</p>
          </GlassCard>
          <GlassCard>
            <h3>⚔️ Master Weapons</h3>
            <p style={{ color: 'var(--text-secondary)' }}>Discover and upgrade dozens of weapons to match your playstyle.</p>
          </GlassCard>
          <GlassCard>
            <h3>🤝 Play Together</h3>
            <p style={{ color: 'var(--text-secondary)' }}>Team up with friends in co-op or battle in dedicated PvP arenas.</p>
          </GlassCard>
        </div>

        {/* Latest Wiki Articles */}
        <section style={{ marginBottom: 'var(--space-3xl)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-lg)' }}>
            <h2>Recently Updated Wiki Articles</h2>
            <Link href="/wiki" style={{ color: 'var(--accent)' }}>View all</Link>
          </div>
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: 'var(--space-md)' }}>
            {wikiArticles.map((article: any) => (
              <GlassCard key={article.id}>
                <Link href={`/wiki/${article.categorySlug}/${article.slug}`}>
                  <h3 style={{ marginBottom: 'var(--space-xs)', color: 'var(--text-primary)' }}>{article.title}</h3>
                </Link>
                <p style={{ fontSize: '14px', color: 'var(--text-muted)' }}>Updated {new Date(article.updatedAt).toLocaleDateString()}</p>
              </GlassCard>
            ))}
          </div>
        </section>

        {/* Latest Forum Threads */}
        <section style={{ marginBottom: 'var(--space-3xl)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-lg)' }}>
            <h2>Active Forum Threads</h2>
            <Link href="/forum" style={{ color: 'var(--accent)' }}>View all</Link>
          </div>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-sm)' }}>
            {forumThreads.map((thread: any) => (
              <GlassCard key={thread.id} style={{ padding: 'var(--space-md)' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <Link href={`/forum/${thread.categorySlug || 'general'}/${thread.id}`}>
                    <h3 style={{ fontSize: '1rem', color: 'var(--text-primary)', margin: 0 }}>{thread.title}</h3>
                  </Link>
                  <span style={{ fontSize: '12px', color: 'var(--text-muted)' }}>{thread.replyCount} replies</span>
                </div>
                <p style={{ fontSize: '12px', color: 'var(--text-secondary)', marginTop: 'var(--space-xs)' }}>
                  By {thread.authorName} • Last active {new Date(thread.lastReplyAt).toLocaleDateString()}
                </p>
              </GlassCard>
            ))}
          </div>
        </section>

        {/* CTA Banner */}
        <section>
          <GlassCard style={{ textAlign: 'center', padding: 'var(--space-2xl)', background: 'var(--accent-light)', borderColor: 'var(--accent)' }}>
            <h2 style={{ marginBottom: 'var(--space-sm)' }}>Ready to join the community?</h2>
            <p style={{ color: 'var(--text-secondary)', marginBottom: 'var(--space-lg)' }}>Create an account to contribute to the wiki and post on the forums.</p>
            <Link href="/auth/register">
              <Button size="lg">Create Account</Button>
            </Link>
          </GlassCard>
        </section>
      </div>
    </div>
  );
}