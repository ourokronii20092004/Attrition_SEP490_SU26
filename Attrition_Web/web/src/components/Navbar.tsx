'use client';
import Link from 'next/link';
import { useAuth } from '@/contexts/AuthContext';
import ThemeToggle from './ThemeToggle';
import { useState } from 'react';
import { FiMenu, FiX } from 'react-icons/fi';

export default function Navbar() {
  const { user, logout } = useAuth();
  const [isOpen, setIsOpen] = useState(false);

  return (
    <nav style={{
      position: 'fixed', top: 0, width: '100%', height: 'var(--nav-height)',
      background: 'var(--glass-bg)', backdropFilter: 'blur(var(--glass-blur))',
      borderBottom: '1px solid var(--glass-border)', zIndex: 1000,
      display: 'flex', alignItems: 'center'
    }}>
      <div className="container" style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', width: '100%' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-lg)' }}>
          <Link href="/" style={{ fontSize: '20px', fontWeight: 'bold', color: 'var(--text-primary)' }}>
            Attrition
          </Link>
          <div className="nav-links" style={{ display: 'flex', gap: 'var(--space-md)' }}>
            <Link href="/" className="nav-link">Home</Link>
            <Link href="/wiki" className="nav-link">Wiki</Link>
            <Link href="/forum" className="nav-link">Forum</Link>
            <Link href="/about" className="nav-link">About</Link>
            {user?.role === 'Admin' && <Link href="/admin" className="nav-link" style={{ color: 'var(--accent)' }}>Admin</Link>}
          </div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-md)' }}>
          <ThemeToggle />
          
          <div className="nav-auth" style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-sm)' }}>
            {user ? (
              <>
                <Link href={`/profile/${user.username}`} className="nav-link">{user.username}</Link>
                <button onClick={logout} className="btn btn-ghost btn-sm">Logout</button>
              </>
            ) : (
              <>
                <Link href="/auth/login" className="btn btn-ghost btn-sm">Login</Link>
                <Link href="/auth/register" className="btn btn-primary btn-sm">Register</Link>
              </>
            )}
          </div>
          
          <button className="mobile-menu-btn" onClick={() => setIsOpen(!isOpen)} style={{ display: 'none', background: 'none', border: 'none', color: 'var(--text-primary)', cursor: 'pointer' }}>
            {isOpen ? <FiX size={24} /> : <FiMenu size={24} />}
          </button>
        </div>
      </div>
      
      {/* Mobile Menu Overlay */}
      {isOpen && (
        <div className="mobile-menu" style={{
          position: 'absolute', top: 'var(--nav-height)', left: 0, width: '100%',
          background: 'var(--bg-secondary)', borderBottom: '1px solid var(--border)',
          display: 'flex', flexDirection: 'column', padding: 'var(--space-md)', gap: 'var(--space-sm)'
        }}>
          <Link href="/" onClick={() => setIsOpen(false)}>Home</Link>
          <Link href="/wiki" onClick={() => setIsOpen(false)}>Wiki</Link>
          <Link href="/forum" onClick={() => setIsOpen(false)}>Forum</Link>
          <Link href="/about" onClick={() => setIsOpen(false)}>About</Link>
          {user?.role === 'Admin' && <Link href="/admin" onClick={() => setIsOpen(false)}>Admin</Link>}
          <hr style={{ borderColor: 'var(--border)', margin: 'var(--space-sm) 0' }} />
          {user ? (
            <>
              <Link href={`/profile/${user.username}`} onClick={() => setIsOpen(false)}>Profile</Link>
              <button onClick={() => { logout(); setIsOpen(false); }} className="btn btn-ghost" style={{ justifyContent: 'flex-start' }}>Logout</button>
            </>
          ) : (
            <>
              <Link href="/auth/login" onClick={() => setIsOpen(false)}>Login</Link>
              <Link href="/auth/register" onClick={() => setIsOpen(false)}>Register</Link>
            </>
          )}
        </div>
      )}
    </nav>
  );
}