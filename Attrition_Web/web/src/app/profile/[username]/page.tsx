import GlassCard from '@/components/GlassCard';
import Avatar from '@/components/Avatar';
import Badge from '@/components/Badge';
import { api } from '@/lib/api';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import ProfileActions from './ProfileActions';

async function getProfile(username: string) {
  try {
    const res = await api.get(`/api/users/${username}/profile`);
    return res.success ? res.data : null;
  } catch {
    return null;
  }
}

export default async function Profile({ params }: { params: { username: string } }) {
  const profile = await getProfile(params.username);

  if (!profile) {
    notFound();
  }

  return (
    <div className="container" style={{ padding: 'var(--space-2xl) 0', maxWidth: '800px' }}>
      <GlassCard style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', textAlign: 'center', gap: 'var(--space-md)' }}>
        <Avatar username={profile.username} src={profile.avatarUrl} size="xl" />
        
        <div>
          <h1 style={{ marginBottom: 'var(--space-xs)' }}>{profile.username}</h1>
          <Badge variant={profile.role === 'Admin' ? 'admin' : 'user'}>{profile.role}</Badge>
        </div>
        
        <ProfileActions profileUsername={profile.username} />

        {profile.bio && (
          <p style={{ color: 'var(--text-secondary)', maxWidth: '500px', lineHeight: 1.6 }}>
            {profile.bio}
          </p>
        )}

        <div style={{ display: 'flex', gap: 'var(--space-xl)', marginTop: 'var(--space-md)', padding: 'var(--space-md) var(--space-xl)', background: 'var(--bg-tertiary)', borderRadius: 'var(--glass-radius-sm)' }}>
          <div>
            <div style={{ fontSize: '24px', fontWeight: 'bold', color: 'var(--text-primary)' }}>{profile.postCount}</div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '1px' }}>Forum Posts</div>
          </div>
          <div>
            <div style={{ fontSize: '24px', fontWeight: 'bold', color: 'var(--text-primary)' }}>{profile.contributionCount}</div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '1px' }}>Wiki Edits</div>
          </div>
          <div>
            <div style={{ fontSize: '24px', fontWeight: 'bold', color: 'var(--text-primary)' }}>
              {new Date(profile.joinedAt).toLocaleDateString(undefined, { month: 'short', year: 'numeric' })}
            </div>
            <div style={{ fontSize: '12px', color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '1px' }}>Joined</div>
          </div>
        </div>
      </GlassCard>
    </div>
  );
}