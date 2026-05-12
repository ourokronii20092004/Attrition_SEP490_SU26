import Link from 'next/link';

export default function Footer() {
  return (
    <footer style={{ borderTop: '1px solid var(--glass-border)', padding: 'var(--space-xl) 0', marginTop: 'auto' }}>
      <div className="container" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-md)', alignItems: 'center' }}>
        <p style={{ color: 'var(--text-muted)' }}>© 2025 Attrition. All rights reserved.</p>
        <div style={{ display: 'flex', gap: 'var(--space-lg)', flexWrap: 'wrap', justifyContent: 'center' }}>
          <Link href="/" style={{ color: 'var(--text-secondary)' }}>Home</Link>
          <Link href="/wiki" style={{ color: 'var(--text-secondary)' }}>Wiki</Link>
          <Link href="/forum" style={{ color: 'var(--text-secondary)' }}>Forum</Link>
          <Link href="/about" style={{ color: 'var(--text-secondary)' }}>About</Link>
          <Link href="/faq" style={{ color: 'var(--text-secondary)' }}>FAQ</Link>
          <Link href="/contact" style={{ color: 'var(--text-secondary)' }}>Contact</Link>
          <Link href="/changelog" style={{ color: 'var(--text-secondary)' }}>Changelog</Link>
        </div>
      </div>
    </footer>
  );
}