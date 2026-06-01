"use client";

import { useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import {
  ArrowLeft, Mail, Shield, Ban, Trash2, KeyRound, CheckCircle2, XCircle, Clock, MapPin, MessageSquare, FileText,
} from "lucide-react";
import { adminApi } from "@/lib/api/admin";
import { useAuth, useToast, useConfirm } from "@/lib/providers";
import { Avatar } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { PageLoader } from "@/components/ui/spinner";
import { EmptyState } from "@/components/ui/empty-state";
import { formatDate } from "@/lib/format-date";
import { qk } from "@/lib/query-keys";

function StatusBadge({ detail }: { detail: { isDeleted: boolean; isBanned: boolean } }) {
  if (detail.isDeleted) return <span className="rounded-full bg-surface-2 px-2.5 py-0.5 text-xs font-medium text-fg-subtle">Deleted</span>;
  if (detail.isBanned) return <span className="rounded-full bg-danger/10 px-2.5 py-0.5 text-xs font-medium text-danger">Banned</span>;
  return <span className="rounded-full bg-success/10 px-2.5 py-0.5 text-xs font-medium text-success">Active</span>;
}

function Field({ icon: Icon, label, value }: { icon: React.ComponentType<{ size?: number; className?: string }>; label: string; value: React.ReactNode }) {
  return (
    <div className="flex items-start gap-2.5">
      <Icon size={15} className="mt-0.5 shrink-0 text-fg-subtle" />
      <div className="min-w-0">
        <dt className="text-xs uppercase tracking-wider text-fg-subtle">{label}</dt>
        <dd className="mt-0.5 break-words text-sm text-fg">{value}</dd>
      </div>
    </div>
  );
}

export default function AdminUserDetailPage() {
  const params = useParams<{ id: string }>();
  const { user: me } = useAuth();
  const { toast } = useToast();
  const confirm = useConfirm();
  const queryClient = useQueryClient();

  const { data: detail, isPending } = useQuery({
    queryKey: qk.admin.userDetail(params.id),
    enabled: me?.role === "Admin" && !!params.id,
    queryFn: async () => {
      const res = await adminApi.getUserDetail(params.id);
      return res.success ? res.data : null;
    },
  });

  const refresh = () => {
    queryClient.invalidateQueries({ queryKey: qk.admin.userDetail(params.id) });
    queryClient.invalidateQueries({ queryKey: qk.admin.users() });
  };

  const banMutation = useMutation({
    mutationFn: () => adminApi.toggleBan(params.id),
    onSuccess: () => { refresh(); toast("Ban status updated.", "success"); },
    onError: () => toast("Failed to update ban status.", "error"),
  });

  const roleMutation = useMutation({
    mutationFn: (role: string) => adminApi.setUserRole(params.id, role),
    onSuccess: () => { refresh(); toast("Role updated.", "success"); },
    onError: () => toast("Failed to update role.", "error"),
  });

  const deleteMutation = useMutation({
    mutationFn: () => adminApi.deleteUser(params.id),
    onSuccess: () => { refresh(); toast("User deleted.", "success"); },
    onError: () => toast("Failed to delete user.", "error"),
  });

  const resetPwMutation = useMutation({
    mutationFn: (pw: string) => adminApi.resetUserPassword(params.id, pw),
    onSuccess: () => toast("Password reset. The user must change it on next login.", "success"),
    onError: () => toast("Failed to reset password.", "error"),
  });

  if (!me || me.role !== "Admin") return null;
  if (isPending) return <PageLoader />;

  if (!detail) {
    return (
      <EmptyState
        title="User not found"
        description="This account may have been removed."
        action={<Link href="/admin/users"><Button variant="secondary">Back to users</Button></Link>}
      />
    );
  }

  const isSelf = detail.id === me.id;

  const onBan = async () => {
    const ok = await confirm({
      title: detail.isBanned ? "Unban user?" : "Ban user?",
      message: detail.isBanned
        ? `Restore access for @${detail.username}?`
        : `Ban @${detail.username}? They'll be signed out and blocked from signing in.`,
      confirmLabel: detail.isBanned ? "Unban" : "Ban",
      danger: !detail.isBanned,
    });
    if (ok) banMutation.mutate();
  };

  const onDelete = async () => {
    const ok = await confirm({
      title: "Delete user?",
      message: `Permanently delete @${detail.username}? This cannot be undone.`,
      confirmLabel: "Delete",
      danger: true,
    });
    if (ok) deleteMutation.mutate();
  };

  const onResetPw = async () => {
    const pw = window.prompt(`Set a temporary password for @${detail.username} (min 8 chars):`);
    if (!pw) return;
    if (pw.length < 8) { toast("Password must be at least 8 characters.", "error"); return; }
    resetPwMutation.mutate(pw);
  };

  return <UserDetailView
    detail={detail} isSelf={isSelf}
    onBan={onBan} onDelete={onDelete} onResetPw={onResetPw}
    onRole={(r) => roleMutation.mutate(r)}
    pending={{ ban: banMutation.isPending, del: deleteMutation.isPending, pw: resetPwMutation.isPending }}
  />;
}

function UserDetailView({ detail, isSelf, onBan, onDelete, onResetPw, onRole, pending }: {
  detail: import("@/lib/types").AdminUserDetailDto;
  isSelf: boolean;
  onBan: () => void; onDelete: () => void; onResetPw: () => void;
  onRole: (role: string) => void;
  pending: { ban: boolean; del: boolean; pw: boolean };
}) {
  return (
    <div>
      <Link href="/admin/users" className="inline-flex items-center gap-1.5 text-sm text-fg-muted transition-colors hover:text-fg">
        <ArrowLeft size={16} /> Users
      </Link>

      <div className="mt-4 flex items-center gap-4">
        <Avatar src={detail.avatarUrl} name={detail.displayName ?? detail.username} size="lg" />
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2">
            <h1 className="font-display text-2xl font-bold text-fg">{detail.displayName ?? detail.username}</h1>
            <StatusBadge detail={detail} />
            {detail.role === "Admin" && (
              <span className="rounded-full bg-accent-soft px-2.5 py-0.5 text-xs font-medium text-accent">Admin</span>
            )}
          </div>
          <p className="mt-0.5 text-sm text-fg-muted">@{detail.username}</p>
          <Link href={`/u/${encodeURIComponent(detail.username)}`} className="mt-1 inline-block text-xs text-accent hover:underline">
            View public profile →
          </Link>
        </div>
      </div>

      <div className="mt-6 grid gap-4 lg:grid-cols-3">
        <Card className="p-4 lg:col-span-2">
          <h2 className="text-sm font-semibold text-fg">Account</h2>
          <dl className="mt-3 grid gap-4 sm:grid-cols-2">
            <Field icon={Mail} label="Email" value={detail.email ?? <span className="text-fg-subtle">—</span>} />
            <Field icon={detail.isEmailVerified ? CheckCircle2 : XCircle} label="Email verified"
              value={detail.isEmailVerified ? "Yes" : (detail.pendingEmail ? `Pending: ${detail.pendingEmail}` : "No")} />
            <Field icon={Shield} label="Auth provider" value={detail.authProvider} />
            <Field icon={Clock} label="Joined" value={formatDate(detail.joinedAt)} />
            <Field icon={Clock} label="Last login" value={detail.lastLoginAt ? formatDate(detail.lastLoginAt) : <span className="text-fg-subtle">Never</span>} />
            <Field icon={MapPin} label="Last login IP" value={detail.lastLoginIp ?? <span className="text-fg-subtle">—</span>} />
            <Field icon={MessageSquare} label="Forum posts" value={detail.postCount} />
            <Field icon={FileText} label="Wiki contributions" value={detail.contributionCount} />
          </dl>
          {detail.bio && (
            <div className="mt-4 border-t border-border pt-3">
              <dt className="text-xs uppercase tracking-wider text-fg-subtle">Bio</dt>
              <dd className="mt-1 whitespace-pre-wrap text-sm text-fg">{detail.bio}</dd>
            </div>
          )}
          {detail.isDeleted && detail.deletedAt && (
            <p className="mt-4 rounded-md bg-surface-2 px-3 py-2 text-xs text-fg-muted">
              Account deleted on {formatDate(detail.deletedAt)}.
            </p>
          )}
        </Card>

        <Card className="p-4">
          <h2 className="text-sm font-semibold text-fg">Moderation</h2>
          {isSelf ? (
            <p className="mt-3 text-sm text-fg-muted">You can't moderate your own account here.</p>
          ) : detail.isDeleted ? (
            <p className="mt-3 text-sm text-fg-muted">This account is deleted; actions are unavailable.</p>
          ) : (
            <div className="mt-3 space-y-3">
              <div>
                <label className="text-xs uppercase tracking-wider text-fg-subtle">Role</label>
                <select
                  value={detail.role}
                  onChange={(e) => onRole(e.target.value)}
                  className="mt-1 w-full rounded-md border border-border bg-surface-2 px-2 py-1.5 text-sm text-fg"
                >
                  <option value="User">User</option>
                  <option value="Admin">Admin</option>
                </select>
              </div>
              <Button variant={detail.isBanned ? "secondary" : "danger"} size="sm" className="w-full" onClick={onBan} loading={pending.ban}>
                <Ban size={14} className="mr-1.5" /> {detail.isBanned ? "Unban user" : "Ban user"}
              </Button>
              <Button variant="secondary" size="sm" className="w-full" onClick={onResetPw} loading={pending.pw}>
                <KeyRound size={14} className="mr-1.5" /> Reset password
              </Button>
              <Button variant="danger" size="sm" className="w-full" onClick={onDelete} loading={pending.del}>
                <Trash2 size={14} className="mr-1.5" /> Delete user
              </Button>
            </div>
          )}
        </Card>
      </div>
    </div>
  );
}
