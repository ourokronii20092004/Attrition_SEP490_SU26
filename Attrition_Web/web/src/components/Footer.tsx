import Link from 'next/link';

const TEAM = [
  { name: 'Phan Phuc Binh', role: 'Lead Client, Game Design, Level Design' },
  { name: 'Nguyen Nhat Dang', role: 'Game Logic, Enemy Design' },
  { name: 'Tran Thien Dang', role: 'Quality Assurance' },
  { name: 'Le Trung Hau', role: 'Backend, Frontend, Game Server' },
];

export default function Footer() {
  return (
    <footer className="footer">
      <div className="container">
        <div className="footer-grid">
          {/* Brand + Team */}
          <div>
            <span className="footer-brand">ATTRITION</span>
            <p className="text-muted" style={{ fontSize: '14px', marginBottom: 'var(--space-md)' }}>
              A souls-like roguelike 2D co-op game. Embrace the darkness, conquer together.
            </p>
            <div className="footer-team">
              {TEAM.map((member) => (
                <div key={member.name} className="footer-team-member">
                  <strong>{member.name}</strong>
                  <br />
                  {member.role}
                </div>
              ))}
            </div>
          </div>

          {/* Navigation */}
          <div className="footer-section">
            <h4>Navigate</h4>
            <Link href="/">Home</Link>
            <Link href="/wiki">Wiki</Link>
            <Link href="/forum">Forum</Link>
            <Link href="/about">About</Link>
          </div>

          {/* Community */}
          <div className="footer-section">
            <h4>Community</h4>
            <Link href="/changelog">Changelog</Link>
            <Link href="/faq">FAQ</Link>
            <Link href="/contact">Contact</Link>
            <Link href="/wiki/contribute">Contribute</Link>
          </div>

          {/* Legal */}
          <div className="footer-section">
            <h4>Account</h4>
            <Link href="/auth/login">Sign In</Link>
            <Link href="/auth/register">Create Account</Link>
          </div>
        </div>

        <div className="footer-bottom">
          <span>© {new Date().getFullYear()} Attrition. SEP490 — Summer 2026. All rights reserved.</span>
          <span className="text-ghost">Built with 🔥 and Next.js</span>
        </div>
      </div>
    </footer>
  );
}