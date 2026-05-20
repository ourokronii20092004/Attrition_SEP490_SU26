import Link from 'next/link';
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
      {/* ═══ HERO SECTION ═══ */}
      <section className="hero">
        <div className="hero-bg" />
        <div className="hero-content">
          <h1 className="hero-title">ATTRITION</h1>
          <p className="hero-subtitle">
            Embrace the Darkness. Conquer Together.
          </p>
          <div className="hero-actions">
            <Link href="/wiki" className="btn btn-primary btn-lg">
              ⚔ Enter the Wiki
            </Link>
            <Link href="/forum" className="btn btn-secondary btn-lg">
              💀 Join the Forum
            </Link>
          </div>
        </div>
      </section>

      <div className="container mt-2xl">
        {/* ═══ FEATURES GRID ═══ */}
        <section className="mb-2xl">
          <div className="features-grid">
            <div className="feature-card">
              <span className="feature-icon">🌍</span>
              <h3>Explore Biomes</h3>
              <p>Procedurally generated dungeons with unique enemies, traps, and ancient secrets hidden in the dark.</p>
            </div>
            <div className="feature-card">
              <span className="feature-icon">⚔️</span>
              <h3>Master Weapons</h3>
              <p>Discover and upgrade dozens of weapons, each with unique movesets to match your playstyle.</p>
            </div>
            <div className="feature-card">
              <span className="feature-icon">🤝</span>
              <h3>Play Together</h3>
              <p>Team up with friends in co-op campaigns or test your skills in dedicated PvP arenas.</p>
            </div>
          </div>
        </section>

        {/* ═══ LATEST WIKI ARTICLES ═══ */}
        {wikiArticles.length > 0 && (
          <section className="mb-2xl">
            <div className="section-header">
              <h2>📜 Recently Updated Wiki</h2>
              <Link href="/wiki">View all →</Link>
            </div>
            <div className="grid-auto">
              {wikiArticles.map((article: any) => (
                <Link
                  key={article.id}
                  href={`/wiki/${article.categorySlug}/${article.slug}`}
                  className="category-card"
                >
                  <h3>{article.title}</h3>
                  <p className="text-muted" style={{ fontSize: '13px', marginTop: 'var(--space-xs)' }}>
                    Updated {new Date(article.updatedAt).toLocaleDateString()}
                  </p>
                </Link>
              ))}
            </div>
          </section>
        )}

        {/* ═══ ACTIVE FORUM THREADS ═══ */}
        {forumThreads.length > 0 && (
          <section className="mb-2xl">
            <div className="section-header">
              <h2>💬 Active Forum Threads</h2>
              <Link href="/forum">View all →</Link>
            </div>
            <div className="thread-list">
              {forumThreads.map((thread: any) => (
                <div key={thread.id} className="thread-item">
                  <div>
                    <Link href={`/forum/${thread.categorySlug || 'general'}/${thread.id}`}>
                      <h4 style={{ margin: 0 }}>{thread.title}</h4>
                    </Link>
                    <p className="text-muted" style={{ fontSize: '13px', marginTop: 'var(--space-xs)' }}>
                      By {thread.authorName} • Last active {new Date(thread.lastReplyAt || thread.createdAt).toLocaleDateString()}
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

        {/* ═══ CTA BANNER ═══ */}
        <section className="mb-2xl">
          <div className="cta-banner">
            <h2>The abyss awaits. Join the hunt.</h2>
            <p>Create an account to contribute to the wiki and post on the forums.</p>
            <Link href="/auth/register" className="btn btn-primary btn-lg">
              🔥 Create Account
            </Link>
          </div>
        </section>
      </div>
    </div>
  );
}