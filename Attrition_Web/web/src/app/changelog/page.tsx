import Breadcrumb from '@/components/Breadcrumb';

const CHANGELOG = [
  {
    version: '0.3.0',
    date: '2026-05-15',
    changes: [
      'Added music player and soundtrack management',
      'New forum post moderation system',
      'Admin dashboard with statistics',
      'Improved wiki article editor with markdown preview',
    ],
  },
  {
    version: '0.2.0',
    date: '2026-04-20',
    changes: [
      'Forum system with categories and threads',
      'Wiki contribution system for community edits',
      'User profile pages with stats',
      'Google OAuth integration',
    ],
  },
  {
    version: '0.1.0',
    date: '2026-03-10',
    changes: [
      'Initial release of the Attrition community hub',
      'Wiki system with categories and articles',
      'User registration and authentication',
      'Dark fantasy UI design system',
    ],
  },
];

export default function ChangelogPage() {
  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Changelog' },
      ]} />

      <h1 className="mb-xl">📋 Changelog</h1>
      <p className="text-muted mb-2xl">Track the latest updates and improvements to the Attrition platform.</p>

      <div className="flex-col gap-xl" style={{ maxWidth: '700px' }}>
        {CHANGELOG.map((release) => (
          <div key={release.version} className="glass-card-static">
            <div className="flex-between mb-md">
              <h2 className="text-ember" style={{ fontSize: '1.5rem', margin: 0 }}>
                v{release.version}
              </h2>
              <span className="badge badge-ember">{release.date}</span>
            </div>
            <ul style={{ paddingLeft: 'var(--space-lg)' }}>
              {release.changes.map((change, i) => (
                <li key={i} className="text-muted" style={{ marginBottom: 'var(--space-xs)', fontSize: '14px' }}>
                  {change}
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
    </div>
  );
}