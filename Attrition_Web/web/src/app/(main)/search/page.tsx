'use client';

import { useState, useEffect, Suspense } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import { formatDate, cn } from '@/lib/utils';

interface SearchWikiResult {
  id: string;
  title: string;
  slug: string;
}

interface SearchUserResult {
  id: string;
  username: string;
  displayName: string | null;
  avatarPath: string | null;
  googleAvatarUrl: string | null;
  role: string;
}

interface SearchPostResult {
  id: string;
  threadId: string;
  content: string;
  threadTitle: string;
  createdAt: string;
}

interface SearchResults {
  wikiResults: SearchWikiResult[];
  userResults: SearchUserResult[];
  postResults: SearchPostResult[];
}

function SearchPageContent() {
  const searchParams = useSearchParams();
  const router = useRouter();
  const toast = useToast();
  const query = searchParams?.get('q') || '';

  const [inputVal, setInputVal] = useState(query);
  const [results, setResults] = useState<SearchResults | null>(null);
  const [loading, setLoading] = useState(false);
  const [activeTab, setActiveTab] = useState<'all' | 'wiki' | 'posts' | 'users'>('all');

  const executeSearch = async (searchQuery: string) => {
    if (!searchQuery.trim()) return;
    setLoading(true);
    try {
      const res = await api.get<SearchResults>(`/search?q=${encodeURIComponent(searchQuery)}`);
      if (res.success && res.data) {
        setResults(res.data);
      }
    } catch (err: any) {
      toast.error(err.message || 'Search execution failed');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (query) {
      setInputVal(query);
      executeSearch(query);
    }
  }, [query]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputVal.trim()) return;
    router.push(`/search?q=${encodeURIComponent(inputVal.trim())}`);
  };

  // Safe search highlight matching function
  const highlightText = (text: string, term: string) => {
    if (!term || !text) return text;
    const cleanTerm = term.replace(/^(wiki|user|post):\s*/i, '').trim();
    if (!cleanTerm) return text;

    const regex = new RegExp(`(${cleanTerm.replace(/[-\/\\^$*+?.()|[\]{}]/g, '\\$&')})`, 'gi');
    const parts = text.split(regex);
    return (
      <>
        {parts.map((part, i) => 
          regex.test(part) ? (
            <mark key={i} style={{ background: 'var(--warning-bg)', color: 'var(--warning)', padding: '0 2px', borderRadius: 'var(--radius-sm)' }}>{part}</mark>
          ) : part
        )}
      </>
    );
  };

  const getWikiUrl = (slug: string) => `/wiki/articles/${slug}`;
  const getThreadUrl = (id: string) => `/forum/thread/${id}`;
  const getUserUrl = (username: string) => `/profile/${username}`;

  const hasResults = results && (
    results.wikiResults.length > 0 ||
    results.userResults.length > 0 ||
    results.postResults.length > 0
  );

  const tabStyle = (isActive: boolean): React.CSSProperties => ({
    paddingBottom: 'var(--space-3)',
    paddingInline: 'var(--space-1)',
    borderBottom: isActive ? '2px solid var(--accent)' : '2px solid transparent',
    color: isActive ? 'var(--accent)' : 'var(--text-tertiary)',
    background: 'none',
    border: 'none',
    borderBottomWidth: 2,
    borderBottomStyle: 'solid',
    borderBottomColor: isActive ? 'var(--accent)' : 'transparent',
    cursor: 'pointer',
    transition: 'color 0.2s, border-color 0.2s'
  });

  const sectionHeaderStyle: React.CSSProperties = {
    fontSize: 'var(--text-xs)',
    fontWeight: 'var(--weight-semibold)' as any,
    textTransform: 'uppercase',
    letterSpacing: '0.05em',
    color: 'var(--text-tertiary)',
    display: 'flex',
    alignItems: 'center',
    gap: 'var(--space-2)',
    marginBottom: 'var(--space-2)'
  };

  return (
    <div className="page container" style={{ maxWidth: 1000 }}>
      {/* Search Bar Input */}
      <form onSubmit={handleSearchSubmit} style={{ display: 'flex', gap: 'var(--space-2)', marginBottom: 'var(--space-8)' }}>
        <div style={{ position: 'relative', flex: 1 }}>
          <input
            type="text"
            className="input"
            style={{ paddingLeft: 'var(--space-12)', fontSize: 'var(--text-lg)' }}
            placeholder="Search wiki, posts, users... e.g. Boss guides, user:Admin..."
            value={inputVal}
            onChange={e => setInputVal(e.target.value)}
          />
          <span style={{ position: 'absolute', left: 'var(--space-4)', top: '50%', transform: 'translateY(-50%)', color: 'var(--text-muted)' }}>🔍</span>
        </div>
        <button 
          type="submit" 
          className="btn btn-primary btn-lg"
          disabled={loading}
        >
          {loading ? 'Searching...' : 'Search'}
        </button>
      </form>

      {/* Advanced Tag Tips */}
      <div
        className="card"
        style={{
          padding: 'var(--space-3)',
          marginBottom: 'var(--space-6)',
          fontSize: 'var(--text-xs)',
          color: 'var(--text-tertiary)',
          display: 'flex',
          alignItems: 'center',
          gap: 'var(--space-2)'
        }}
      >
        <span style={{ color: 'var(--accent)' }}>#</span>
        <span>
          <strong>ProTip:</strong> Scope searches using filters like{' '}
          <code style={{ color: 'var(--accent)', fontFamily: 'var(--font-mono)' }}>wiki:Boss</code>,{' '}
          <code style={{ color: 'var(--accent)', fontFamily: 'var(--font-mono)' }}>user:Admin</code>, or{' '}
          <code style={{ color: 'var(--accent)', fontFamily: 'var(--font-mono)' }}>post:Lore</code>.
        </span>
      </div>

      {loading ? (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', padding: 'var(--space-20) 0' }}>
          <div className="skeleton" style={{ width: 32, height: 32, borderRadius: '50%' }} />
        </div>
      ) : !results ? (
        <div className="empty-state">
          <span className="empty-state-icon">🔍</span>
          <h3>Start exploring the Attrition Universe</h3>
          <p>Type a search query above to browse guides, players, and forum threads.</p>
        </div>
      ) : !hasResults ? (
        <div className="empty-state">
          <span className="empty-state-icon">❓</span>
          <h3>No results found for &ldquo;{query}&rdquo;</h3>
          <p>Double check spelling or try adding advanced filters.</p>
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-6)' }}>
          {/* Tab selector */}
          <div style={{ display: 'flex', borderBottom: '1px solid var(--border)', gap: 'var(--space-6)', fontSize: 'var(--text-sm)', fontWeight: 'var(--weight-semibold)' as any, marginBottom: 'var(--space-4)' }}>
            <button onClick={() => setActiveTab('all')} style={tabStyle(activeTab === 'all')}>
              All Results
            </button>
            {results.wikiResults.length > 0 && (
              <button onClick={() => setActiveTab('wiki')} style={tabStyle(activeTab === 'wiki')}>
                Wiki ({results.wikiResults.length})
              </button>
            )}
            {results.postResults.length > 0 && (
              <button onClick={() => setActiveTab('posts')} style={tabStyle(activeTab === 'posts')}>
                Forum ({results.postResults.length})
              </button>
            )}
            {results.userResults.length > 0 && (
              <button onClick={() => setActiveTab('users')} style={tabStyle(activeTab === 'users')}>
                Users ({results.userResults.length})
              </button>
            )}
          </div>

          {/* Results Grid */}
          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-6)' }}>
            {/* Wiki results */}
            {(activeTab === 'all' || activeTab === 'wiki') && results.wikiResults.length > 0 && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
                <h3 style={sectionHeaderStyle}>
                  <span style={{ color: 'var(--accent)' }}>📖</span>
                  Wiki Lore Guides
                </h3>
                <div style={{ display: 'grid', gap: 'var(--space-3)' }}>
                  {results.wikiResults.map(art => (
                    <Link 
                      key={art.id} 
                      href={getWikiUrl(art.slug)}
                      className="card card-hoverable"
                      style={{ padding: 'var(--space-4)', display: 'block', textDecoration: 'none' }}
                    >
                      <strong style={{ color: 'var(--text)', fontSize: 'var(--text-lg)', display: 'block' }}>
                        {highlightText(art.title, query)}
                      </strong>
                      <span className="text-xs" style={{ color: 'var(--text-muted)', display: 'block', marginTop: 'var(--space-1)' }}>
                        attrition.wiki/articles/{art.slug}
                      </span>
                    </Link>
                  ))}
                </div>
              </div>
            )}

            {/* Forum post results */}
            {(activeTab === 'all' || activeTab === 'posts') && results.postResults.length > 0 && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
                <h3 style={sectionHeaderStyle}>
                  <span style={{ color: 'var(--accent)' }}>💬</span>
                  Forum Posts & Discussions
                </h3>
                <div style={{ display: 'grid', gap: 'var(--space-3)' }}>
                  {results.postResults.map(post => (
                    <Link 
                      key={post.id} 
                      href={getThreadUrl(post.threadId)}
                      className="card card-hoverable"
                      style={{ padding: 'var(--space-4)', display: 'block', textDecoration: 'none' }}
                    >
                      <strong style={{ color: 'var(--accent)', display: 'block', marginBottom: 'var(--space-1)' }}>
                        {post.threadTitle}
                      </strong>
                      <p style={{ color: 'var(--text-secondary)', fontSize: 'var(--text-sm)', fontStyle: 'italic', lineHeight: 'var(--leading-relaxed)', maxHeight: 80, overflow: 'hidden', marginBottom: 'var(--space-2)' }}>
                        &ldquo;{highlightText(post.content.slice(0, 200) + (post.content.length > 200 ? '...' : ''), query)}&rdquo;
                      </p>
                      <span className="text-xs" style={{ color: 'var(--text-muted)', display: 'block' }}>{formatDate(post.createdAt)}</span>
                    </Link>
                  ))}
                </div>
              </div>
            )}

            {/* User results */}
            {(activeTab === 'all' || activeTab === 'users') && results.userResults.length > 0 && (
              <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-3)' }}>
                <h3 style={sectionHeaderStyle}>
                  <span style={{ color: 'var(--accent)' }}>👤</span>
                  Attrition Players
                </h3>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(300px, 1fr))', gap: 'var(--space-4)' }}>
                  {results.userResults.map(u => {
                    const avatarUrl = u.avatarPath 
                      ? (u.avatarPath.startsWith("http") ? u.avatarPath : `https://attrition.hault.io.vn${u.avatarPath}`) 
                      : u.googleAvatarUrl;
                    
                    return (
                      <Link 
                        key={u.id} 
                        href={getUserUrl(u.username)}
                        className="card card-hoverable"
                        style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-4)', padding: 'var(--space-4)', textDecoration: 'none' }}
                      >
                        <div className="avatar avatar-md" style={{ background: 'var(--accent-subtle)', color: 'var(--accent)', flexShrink: 0 }}>
                          {avatarUrl ? (
                            // eslint-disable-next-line @next/next/no-img-element
                            <img src={avatarUrl} alt="" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                          ) : (
                            u.username[0].toUpperCase()
                          )}
                        </div>
                        <div>
                          <strong style={{ color: 'var(--text)', display: 'block' }}>
                            {highlightText(u.displayName || u.username, query)}
                          </strong>
                          <span className="text-xs" style={{ color: 'var(--text-muted)' }}>@{u.username}</span>
                          <span className="badge badge-accent" style={{ marginLeft: 'var(--space-2)', fontSize: 10, textTransform: 'uppercase' }}>
                            {u.role}
                          </span>
                        </div>
                      </Link>
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export default function SearchPage() {
  return (
    <Suspense fallback={
      <div className="page container" style={{ display: 'flex', justifyContent: 'center', alignItems: 'center' }}>
        <div className="skeleton" style={{ width: 32, height: 32, borderRadius: '50%' }} />
      </div>
    }>
      <SearchPageContent />
    </Suspense>
  );
}
