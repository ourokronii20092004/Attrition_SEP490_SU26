'use client';
import { useState, useEffect } from 'react';
import GlassCard from '@/components/GlassCard';
import Breadcrumb from '@/components/Breadcrumb';
import SearchBar from '@/components/SearchBar';
import Pagination from '@/components/Pagination';
import { api } from '@/lib/api';
import Link from 'next/link';

export default function WikiCategory({ params }: { params: { category: string } }) {
  const [articles, setArticles] = useState<any[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [loading, setLoading] = useState(true);

  const fetchArticles = async () => {
    setLoading(true);
    try {
      const res = await api.get(`/api/wiki/articles?category=${params.category}&search=${search}&page=${page}`);
      if (res.items) {
        setArticles(res.items);
        setTotal(res.totalCount);
      }
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchArticles();
  }, [params.category, search, page]);

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0' }}>
      <Breadcrumb items={[
        { label: 'Home', href: '/' }, 
        { label: 'Wiki', href: '/wiki' },
        { label: params.category.charAt(0).toUpperCase() + params.category.slice(1).replace('-', ' ') }
      ]} />
      
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 'var(--space-xl)', flexWrap: 'wrap', gap: 'var(--space-md)' }}>
        <h1 style={{ margin: 0 }}>
          {params.category.charAt(0).toUpperCase() + params.category.slice(1).replace('-', ' ')}
        </h1>
        <SearchBar value={search} onChange={(val) => { setSearch(val); setPage(1); }} placeholder="Search articles..." />
      </div>

      {loading ? (
        <div style={{ textAlign: 'center', padding: 'var(--space-3xl)' }}>Loading...</div>
      ) : articles.length > 0 ? (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)' }}>
          {articles.map((article) => (
            <Link href={`/wiki/${params.category}/${article.slug}`} key={article.id} style={{ textDecoration: 'none' }}>
              <GlassCard style={{ transition: 'all var(--transition-base)' }}>
                <h3 style={{ margin: '0 0 var(--space-xs) 0', color: 'var(--text-primary)' }}>{article.title}</h3>
                <div style={{ fontSize: '14px', color: 'var(--text-secondary)' }}>
                  <span>Last updated: {new Date(article.updatedAt).toLocaleDateString()}</span>
                  {article.authorName && <span style={{ marginLeft: 'var(--space-md)' }}>By {article.authorName}</span>}
                </div>
              </GlassCard>
            </Link>
          ))}
          <Pagination currentPage={page} totalPages={Math.ceil(total / 20)} onPageChange={setPage} />
        </div>
      ) : (
        <GlassCard style={{ textAlign: 'center', padding: 'var(--space-3xl)' }}>
          <p style={{ color: 'var(--text-secondary)' }}>No articles found in this category.</p>
        </GlassCard>
      )}
    </div>
  );
}