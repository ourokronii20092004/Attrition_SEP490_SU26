'use client';
import { useState, useEffect } from 'react';
import Breadcrumb from '@/components/Breadcrumb';
import Avatar from '@/components/Avatar';
import Badge from '@/components/Badge';
import { api } from '@/lib/api';

export default function ProfilePage({ params }: { params: { username: string } }) {
  const [profile, setProfile] = useState<any>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchProfile = async () => {
      const res = await api.get(`/api/users/${params.username}`);
      if (res.success) {
        setProfile(res.data);
      }
      setLoading(false);
    };
    fetchProfile();
  }, [params.username]);

  if (loading) {
    return (
      <div className="loading-screen">
        <div className="spinner spinner-lg" />
      </div>
    );
  }

  if (!profile) {
    return (
      <div className="container">
        <div className="empty-state">
          <div className="empty-state-icon">👤</div>
          <h3>User not found</h3>
          <p>The user &quot;{params.username}&quot; does not exist.</p>
        </div>
      </div>
    );
  }

  return (
    <div className="container">
      <Breadcrumb items={[
        { label: 'Home', href: '/' },
        { label: 'Profile' },
        { label: profile.username },
      ]} />

      <div className="glass-card-static" style={{ maxWidth: '800px', margin: '0 auto' }}>
        <div className="profile-header">
          <Avatar src={profile.avatarUrl} alt={profile.username} size="xl" />
          <div className="profile-info">
            <h1>{profile.username}</h1>
            <Badge variant={profile.role === 'Admin' ? 'admin' : 'user'}>
              {profile.role}
            </Badge>
            {profile.bio && (
              <p className="text-muted mt-md">{profile.bio}</p>
            )}
            <div className="profile-stats">
              <div className="profile-stat">
                <div className="profile-stat-value">{profile.postCount || 0}</div>
                <div className="profile-stat-label">Posts</div>
              </div>
              <div className="profile-stat">
                <div className="profile-stat-value">{profile.threadCount || 0}</div>
                <div className="profile-stat-label">Threads</div>
              </div>
              <div className="profile-stat">
                <div className="profile-stat-value">{profile.contributionCount || 0}</div>
                <div className="profile-stat-label">Wiki Edits</div>
              </div>
            </div>
          </div>
        </div>

        <div style={{ borderTop: '1px solid var(--border-dim)', paddingTop: 'var(--space-lg)', marginTop: 'var(--space-lg)' }}>
          <p className="text-muted" style={{ fontSize: '14px' }}>
            Member since {new Date(profile.createdAt).toLocaleDateString()}
          </p>
        </div>
      </div>
    </div>
  );
}