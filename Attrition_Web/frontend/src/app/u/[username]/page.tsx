"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import { accountApi } from "@/lib/api/account";
import { resolveMediaUrl } from "@/lib/api/media";
import { PageLoader } from "@/components/ui/spinner";
import type { UserDto } from "@/lib/types";

export default function ProfilePage() {
  const params = useParams<{ username: string }>();
  const [profile, setProfile] = useState<UserDto | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!params.username) return;
    let ignore = false;
    setLoading(true);
    accountApi
      .getProfile(params.username)
      .then((res) => {
        if (!ignore && res.success) setProfile(res.data);
      })
      .finally(() => { if (!ignore) setLoading(false); });
    return () => { ignore = true; };
  }, [params.username]);

  if (loading) return <PageLoader />;
  if (!profile) {
    return (
      <div className="mx-auto max-w-3xl px-4 py-12 text-center">
        <h1 className="font-display text-3xl font-bold text-fg">User Not Found</h1>
      </div>
    );
  }

  return (
    <div className="mx-auto max-w-3xl px-4 py-8">
      {profile.backgroundUrl && (
        <div className="relative -mx-4 -mt-8 mb-6 h-48 overflow-hidden rounded-b-xl">
          <img src={resolveMediaUrl(profile.backgroundUrl) ?? ""} alt="" className="h-full w-full object-cover" />
          <div className="absolute inset-0 bg-gradient-to-t from-bg/80 to-transparent" />
        </div>
      )}

      <div className="flex items-center gap-4">
        {profile.avatarUrl ? (
          <img src={resolveMediaUrl(profile.avatarUrl) ?? ""} alt="" className="h-20 w-20 rounded-full border-4 border-surface object-cover" />
        ) : (
          <div className="flex h-20 w-20 items-center justify-center rounded-full bg-surface-3 text-2xl font-bold text-fg-muted">
            {(profile.displayName ?? profile.username)[0]}
          </div>
        )}
        <div>
          <h1 className="font-display text-3xl font-bold text-fg">{profile.displayName ?? profile.username}</h1>
          <p className="text-fg-muted">@{profile.username}</p>
          <div className="mt-1 flex items-center gap-3 text-sm text-fg-subtle">
            <span>{profile.role}</span>
            <span>Joined {new Date(profile.joinedAt).toLocaleDateString()}</span>
          </div>
        </div>
      </div>

      {profile.bio && <p className="mt-6 text-fg-muted leading-relaxed">{profile.bio}</p>}

      <div className="mt-6 grid grid-cols-2 gap-4">
        <div className="card p-4 text-center">
          <p className="text-2xl font-bold text-fg">{profile.postCount}</p>
          <p className="text-sm text-fg-muted">Posts</p>
        </div>
        <div className="card p-4 text-center">
          <p className="text-2xl font-bold text-fg">{profile.contributionCount}</p>
          <p className="text-sm text-fg-muted">Contributions</p>
        </div>
      </div>
    </div>
  );
}
