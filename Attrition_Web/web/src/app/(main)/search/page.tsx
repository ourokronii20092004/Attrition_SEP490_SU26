'use client';

import { useState, useEffect, Suspense } from 'react';
import { useSearchParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { api } from '@/lib/api';
import { useToast } from '@/contexts/ToastContext';
import { formatDate } from '@/lib/utils';
import { 
  Search, 
  BookOpen, 
  MessageSquare, 
  User, 
  HelpCircle,
  Hash
} from 'lucide-react';

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
    // Extract actual search term in case tags exist (e.g. wiki:boss -> boss)
    const cleanTerm = term.replace(/^(wiki|user|post):\s*/i, '').trim();
    if (!cleanTerm) return text;

    const regex = new RegExp(`(${cleanTerm.replace(/[-\/\\^$*+?.()|[\]{}]/g, '\\$&')})`, 'gi');
    const parts = text.split(regex);
    return (
      <>
        {parts.map((part, i) => 
          regex.test(part) ? (
            <mark key={i} className="bg-yellow-500/30 text-yellow-200 px-0.5 rounded">{part}</mark>
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

  return (
    <div className="container" style={{ maxWidth: '1000px', padding: '40px 20px', color: '#f3f4f6' }}>
      {/* Search Bar Input */}
      <form onSubmit={handleSearchSubmit} className="flex gap-2 mb-8">
        <div className="relative flex-1">
          <input
            type="text"
            className="w-full bg-slate-900 border border-slate-800 rounded-md py-3 pl-12 pr-4 text-white placeholder-gray-500 focus:outline-none focus:border-indigo-500 text-lg"
            placeholder="Search wiki, posts, users... e.g. Boss guides, user:Admin..."
            value={inputVal}
            onChange={e => setInputVal(e.target.value)}
          />
          <Search className="absolute left-4 top-3.5 text-gray-500" size={22} />
        </div>
        <button 
          type="submit" 
          className="btn btn-primary bg-gradient-to-r from-indigo-600 to-purple-600 hover:from-indigo-500 hover:to-purple-500 px-8 text-lg font-bold"
          disabled={loading}
        >
          {loading ? 'Searching...' : 'Search'}
        </button>
      </form>

      {/* Advanced Tag Tips */}
      <div className="bg-slate-900/40 border border-slate-800/80 rounded-lg p-3 mb-6 text-xs text-gray-400 flex items-center gap-2">
        <Hash size={14} className="text-indigo-400" />
        <span><strong>ProTip:</strong> Scope searches using filters like <code className="text-indigo-300 font-mono">wiki:Boss</code>, <code className="text-indigo-300 font-mono">user:Admin</code>, or <code className="text-indigo-300 font-mono">post:Lore</code>.</span>
      </div>

      {loading ? (
        <div className="flex justify-center items-center py-20">
          <div className="spinner-border animate-spin inline-block w-8 h-8 border-4 rounded-full text-indigo-500" role="status"></div>
        </div>
      ) : !results ? (
        <div className="text-center py-20 text-gray-500">
          <Search size={48} className="mx-auto mb-3 opacity-30 text-indigo-300" />
          <p className="text-lg font-semibold">Start exploring the Attrition Universe</p>
          <p className="text-sm">Type a search query above to browse guides, players, and forum threads.</p>
        </div>
      ) : !hasResults ? (
        <div className="text-center py-20 text-gray-500">
          <HelpCircle size={48} className="mx-auto mb-3 opacity-30" />
          <p className="text-lg font-semibold">No results found for "{query}"</p>
          <p className="text-sm mt-1">Double check spelling or try adding advanced filters.</p>
        </div>
      ) : (
        <div className="flex flex-col gap-6">
          {/* Tab selector */}
          <div className="flex border-b border-slate-850 gap-6 text-sm font-semibold mb-4">
            <button 
              onClick={() => setActiveTab('all')}
              className={`pb-3 px-1 border-b-2 ${activeTab === 'all' ? 'border-indigo-500 text-indigo-400' : 'border-transparent text-gray-400 hover:text-white'}`}
            >
              All Results
            </button>
            {results.wikiResults.length > 0 && (
              <button 
                onClick={() => setActiveTab('wiki')}
                className={`pb-3 px-1 border-b-2 ${activeTab === 'wiki' ? 'border-indigo-500 text-indigo-400' : 'border-transparent text-gray-400 hover:text-white'}`}
              >
                Wiki ({results.wikiResults.length})
              </button>
            )}
            {results.postResults.length > 0 && (
              <button 
                onClick={() => setActiveTab('posts')}
                className={`pb-3 px-1 border-b-2 ${activeTab === 'posts' ? 'border-indigo-500 text-indigo-400' : 'border-transparent text-gray-400 hover:text-white'}`}
              >
                Forum ({results.postResults.length})
              </button>
            )}
            {results.userResults.length > 0 && (
              <button 
                onClick={() => setActiveTab('users')}
                className={`pb-3 px-1 border-b-2 ${activeTab === 'users' ? 'border-indigo-500 text-indigo-400' : 'border-transparent text-gray-400 hover:text-white'}`}
              >
                Users ({results.userResults.length})
              </button>
            )}
          </div>

          {/* Results Grid */}
          <div className="flex flex-col gap-6">
            {/* Wiki results */}
            {(activeTab === 'all' || activeTab === 'wiki') && results.wikiResults.length > 0 && (
              <div className="flex flex-col gap-3">
                <h3 className="text-xs font-semibold uppercase tracking-wider text-gray-400 flex items-center gap-2 mb-2">
                  <BookOpen size={16} className="text-indigo-400" />
                  Wiki Lore Guides
                </h3>
                <div className="grid gap-3">
                  {results.wikiResults.map(art => (
                    <Link 
                      key={art.id} 
                      href={getWikiUrl(art.slug)}
                      className="block p-4 rounded-xl border border-slate-800 bg-slate-900/30 hover:border-indigo-900/40 hover:bg-indigo-950/5 transition"
                    >
                      <strong className="text-white text-lg block hover:text-indigo-300 transition">
                        {highlightText(art.title, query)}
                      </strong>
                      <span className="text-xs text-gray-500 block mt-1">attrition.wiki/articles/{art.slug}</span>
                    </Link>
                  ))}
                </div>
              </div>
            )}

            {/* Forum post results */}
            {(activeTab === 'all' || activeTab === 'posts') && results.postResults.length > 0 && (
              <div className="flex flex-col gap-3">
                <h3 className="text-xs font-semibold uppercase tracking-wider text-gray-400 flex items-center gap-2 mb-2">
                  <MessageSquare size={16} className="text-indigo-400" />
                  Forum Posts & Discussions
                </h3>
                <div className="grid gap-3">
                  {results.postResults.map(post => (
                    <Link 
                      key={post.id} 
                      href={getThreadUrl(post.threadId)}
                      className="block p-4 rounded-xl border border-slate-800 bg-slate-900/30 hover:border-indigo-900/40 hover:bg-indigo-950/5 transition"
                    >
                      <strong className="text-indigo-300 block text-md mb-1 hover:text-white transition">
                        {post.threadTitle}
                      </strong>
                      <p className="text-gray-300 text-sm italic font-medium leading-relaxed max-h-20 overflow-hidden text-ellipsis mb-2">
                        "{highlightText(post.content.slice(0, 200) + (post.content.length > 200 ? '...' : ''), query)}"
                      </p>
                      <span className="text-xs text-gray-500 block">{formatDate(post.createdAt)}</span>
                    </Link>
                  ))}
                </div>
              </div>
            )}

            {/* User results */}
            {(activeTab === 'all' || activeTab === 'users') && results.userResults.length > 0 && (
              <div className="flex flex-col gap-3">
                <h3 className="text-xs font-semibold uppercase tracking-wider text-gray-400 flex items-center gap-2 mb-2">
                  <User size={16} className="text-indigo-400" />
                  Attrition Players
                </h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {results.userResults.map(u => {
                    const avatarUrl = u.avatarPath 
                      ? (u.avatarPath.startsWith("http") ? u.avatarPath : `https://attrition.hault.io.vn${u.avatarPath}`) 
                      : u.googleAvatarUrl;
                    
                    return (
                      <Link 
                        key={u.id} 
                        href={getUserUrl(u.username)}
                        className="flex items-center gap-4 p-4 rounded-xl border border-slate-800 bg-slate-900/30 hover:border-indigo-900/40 hover:bg-indigo-950/5 transition"
                      >
                        <div className="w-12 h-12 rounded-full bg-indigo-950 flex items-center justify-center font-bold text-indigo-300 border border-indigo-900/50 overflow-hidden flex-shrink-0">
                          {avatarUrl ? (
                            <img src={avatarUrl} alt="" className="w-full h-full object-cover" />
                          ) : (
                            u.username[0].toUpperCase()
                          )}
                        </div>
                        <div>
                          <strong className="text-white block hover:text-indigo-300 transition">
                            {highlightText(u.displayName || u.username, query)}
                          </strong>
                          <span className="text-xs text-gray-500">@{u.username}</span>
                          <span className="ml-2 inline-block px-1.5 py-0.5 rounded text-[10px] uppercase font-bold bg-indigo-950 text-indigo-400 border border-indigo-900/40">
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
      <div className="flex justify-center items-center py-20 bg-slate-950 min-h-screen">
        <div className="spinner-border animate-spin inline-block w-8 h-8 border-4 rounded-full text-indigo-500" role="status"></div>
      </div>
    }>
      <SearchPageContent />
    </Suspense>
  );
}
