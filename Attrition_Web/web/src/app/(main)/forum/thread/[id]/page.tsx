"use client";

import { useEffect, useState, useCallback } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { api } from "@/lib/api";
import { useAuth } from "@/contexts/AuthContext";
import { useToast } from "@/contexts/ToastContext";
import { timeAgo, getInitials } from "@/lib/utils";
import { ThumbsUp, ThumbsDown, Pencil, Trash2, Lock, MessageCircle, Pin } from "lucide-react";
import styles from "../../forum.module.css";

interface ThreadDetail {
  id: string;
  title: string;
  categorySlug: string;
  categoryName: string;
  authorName: string;
  createdAt: string;
  isPinned: boolean;
  isLocked: boolean;
  replyCount: number;
}

interface Post {
  id: string;
  threadId: string;
  content: string;
  authorName: string;
  authorAvatar: string | null;
  authorRole: string;
  createdAt: string;
  updatedAt: string | null;
  likeCount: number;
  dislikeCount: number;
  currentUserReaction: string | null;
}

function extractData<T>(res: unknown): T | null {
  const r = res as Record<string, unknown>;
  if (r.success && r.data) return r.data as T;
  if (r.items && Array.isArray(r.items)) return r.items as T;
  if (Array.isArray(r)) return r as T;
  if (r.id) return r as T;
  return null;
}

import { Pagination } from "@/components/Pagination";

export default function ForumThreadPage() {
  const params = useParams();
  const router = useRouter();
  const threadId = params.id as string;
  const { isAuthenticated, user } = useAuth();
  const toast = useToast();

  const [thread, setThread] = useState<ThreadDetail | null>(null);
  const [posts, setPosts] = useState<Post[]>([]);
  const [loading, setLoading] = useState(true);
  const [replyContent, setReplyContent] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [editingPostId, setEditingPostId] = useState<string | null>(null);
  const [editContent, setEditContent] = useState("");
  const [deletingThread, setDeletingThread] = useState(false);
  
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPosts, setTotalPosts] = useState(0);

  const fetchThread = useCallback(async () => {
    try {
      // Use URLSearchParams for client-side pagination
      const urlParams = new URLSearchParams(window.location.search);
      const page = parseInt(urlParams.get("page") || "1");
      setCurrentPage(page);

      const [threadRes, postsRes] = await Promise.all([
        api.get<ThreadDetail>(`/forum/threads/${threadId}`),
        api.get<any>(`/forum/threads/${threadId}/posts?page=${page}&pageSize=20`),
      ]);
      const threadData = extractData<ThreadDetail>(threadRes);
      if (threadData) setThread(threadData);

      const pData = postsRes as any;
      if (pData.items) {
        setPosts(pData.items);
        setTotalPosts(pData.totalCount || 0);
      } else if (pData.data?.items) {
        setPosts(pData.data.items);
        setTotalPosts(pData.data.totalCount || 0);
      } else {
        const postsData = extractData<Post[]>(postsRes);
        if (postsData) {
          setPosts(postsData);
          setTotalPosts(postsData.length);
        }
      }
    } catch {
      /* ignore */
    } finally {
      setLoading(false);
    }
  }, [threadId]);

  useEffect(() => { fetchThread(); }, [fetchThread]);

  const refreshPosts = async () => {
    const postsRes = await api.get<any>(`/forum/threads/${threadId}/posts?page=${currentPage}&pageSize=20`);
    const pData = postsRes as any;
    if (pData.items) {
      setPosts(pData.items);
      setTotalPosts(pData.totalCount || 0);
    } else if (pData.data?.items) {
      setPosts(pData.data.items);
      setTotalPosts(pData.data.totalCount || 0);
    } else {
      const postsData = extractData<Post[]>(postsRes);
      if (postsData) setPosts(postsData);
    }
  };

  const handleReact = async (postId: string, type: string) => {
    if (!isAuthenticated) {
      toast.info("Sign in to react");
      return;
    }
    try {
      await api.post(`/forum/posts/${postId}/react`, { reactionType: type.toLowerCase() });
      await refreshPosts();
    } catch {
      toast.error("Failed to react");
    }
  };

  const handleReply = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!replyContent.trim()) return;
    setSubmitting(true);
    try {
      await api.post(`/forum/threads/${threadId}/posts`, { content: replyContent });
      setReplyContent("");
      toast.success("Reply posted");
      await refreshPosts();
    } catch {
      toast.error("Failed to post reply");
    } finally {
      setSubmitting(false);
    }
  };

  const handleEditPost = async (postId: string) => {
    if (!editContent.trim()) return;
    try {
      await api.put(`/forum/posts/${postId}`, { content: editContent.trim() });
      setEditingPostId(null);
      setEditContent("");
      toast.success("Post updated");
      await refreshPosts();
    } catch {
      toast.error("Failed to update post");
    }
  };

  const handleDeletePost = async (postId: string) => {
    if (!confirm("Delete this post?")) return;
    try {
      await api.delete(`/forum/posts/${postId}`);
      toast.success("Post deleted");
      await refreshPosts();
    } catch {
      toast.error("Failed to delete post");
    }
  };

  const handleDeleteThread = async () => {
    if (!confirm("Delete this entire thread? This cannot be undone.")) return;
    setDeletingThread(true);
    try {
      await api.delete(`/forum/threads/${threadId}`);
      toast.success("Thread deleted");
      router.push("/forum");
    } catch {
      toast.error("Failed to delete thread");
      setDeletingThread(false);
    }
  };

  const isOwner = (authorName: string) =>
    user?.username?.toLowerCase() === authorName.toLowerCase();

  const isAdmin = user?.role === "Admin";

  if (loading) {
    return (
      <div className="page">
        <div className="container">
          <div className="skeleton skeleton-heading" style={{ width: "60%", marginBottom: "var(--space-6)" }} />
          {Array.from({ length: 3 }).map((_, i) => (
            <div key={i} style={{ background: "var(--surface)", border: "1px solid var(--border)", borderRadius: "var(--radius-lg)", padding: "var(--space-5)", marginBottom: "var(--space-4)" }}>
              <div className="skeleton skeleton-text" style={{ width: "30%" }} />
              <div className="skeleton skeleton-text" style={{ width: "80%", marginTop: "var(--space-3)" }} />
            </div>
          ))}
        </div>
      </div>
    );
  }

  if (!thread) {
    return (
      <div className="page">
        <div className="container">
          <div className="empty-state">
            <span className="empty-state-icon"><MessageCircle size={40} /></span>
            <h3>Thread not found</h3>
            <p>This thread may have been deleted.</p>
            <Link href="/forum" className="btn btn-primary btn-md" style={{ marginTop: "var(--space-4)" }}>Back to Forum</Link>
          </div>
        </div>
      </div>
    );
  }

  const categoryName = thread.categoryName || thread.categorySlug?.replace(/-/g, " ").replace(/\b\w/g, (c: string) => c.toUpperCase()) || "Forum";

  return (
    <div className="page">
      <div className="container">
        {/* Breadcrumb */}
        <div className="breadcrumb" style={{ marginBottom: "var(--space-6)" }}>
          <Link href="/">Home</Link>
          <span className="breadcrumb-separator">›</span>
          <Link href="/forum">Forum</Link>
          <span className="breadcrumb-separator">›</span>
          <Link href={`/forum/${thread.categorySlug}`}>{categoryName}</Link>
          <span className="breadcrumb-separator">›</span>
          <span className="breadcrumb-current">Thread</span>
        </div>

        {/* Thread Header */}
        <div className={styles.threadHeader}>
          <div style={{ display: "flex", alignItems: "flex-start", justifyContent: "space-between", gap: "var(--space-4)" }}>
            <h1 className={styles.threadDetailTitle}>
              {thread.isPinned && <><Pin style={{ display: "inline", marginRight: 6, color: "var(--accent)" }} size={18} /> </>}
              {thread.isLocked && <><Lock style={{ display: "inline", marginRight: 6, color: "var(--text-muted)" }} size={18} /> </>}
              {thread.title}
            </h1>
            {(isOwner(thread.authorName) || isAdmin) && (
              <button
                onClick={handleDeleteThread}
                disabled={deletingThread}
                className="btn btn-danger btn-sm"
                style={{ flexShrink: 0 }}
              >
                {deletingThread ? "Deleting..." : "Delete Thread"}
              </button>
            )}
          </div>
          <div className={styles.threadDetailMeta}>
            <span>by <Link href={`/profile/${thread.authorName}`} style={{ color: "var(--accent)" }}>{thread.authorName}</Link></span>
            <span>·</span>
            <span>{timeAgo(thread.createdAt)}</span>
            <span>·</span>
            <span>{posts.length} {posts.length === 1 ? "reply" : "replies"}</span>
          </div>
        </div>

        {/* Posts */}
        <div className={styles.postList}>
          {posts.map((post) => {
            const avatarUrl = post.authorAvatar
              ? (post.authorAvatar.startsWith("http") ? post.authorAvatar : `https://attrition.hault.io.vn${post.authorAvatar}`)
              : null;
            const canEdit = isOwner(post.authorName);
            const canDelete = isOwner(post.authorName) || isAdmin;

            return (
              <div key={post.id} className={styles.post}>
                <div className={styles.postHeader}>
                  <div className={styles.postAuthor}>
                    <div style={{
                      width: 32, height: 32, borderRadius: "50%",
                      background: "var(--accent-subtle)", display: "flex",
                      alignItems: "center", justifyContent: "center",
                      fontSize: "var(--text-xs)", fontWeight: "var(--weight-bold)",
                      color: "var(--accent)", overflow: "hidden", flexShrink: 0
                    }}>
                      {avatarUrl ? (
                        <img src={avatarUrl} alt="" style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                      ) : (
                        getInitials(post.authorName)
                      )}
                    </div>
                    <div>
                      <Link href={`/profile/${post.authorName}`} className={styles.postAuthorName}>
                        {post.authorName}
                        {post.authorRole === "Admin" && (
                          <span style={{ marginLeft: "var(--space-2)", fontSize: "var(--text-xs)", color: "var(--accent)", fontWeight: "var(--weight-normal)" }}>Admin</span>
                        )}
                      </Link>
                      <div className={styles.postDate}>{timeAgo(post.createdAt)}{post.updatedAt && " (edited)"}</div>
                    </div>
                  </div>
                  {/* Edit / Delete actions */}
                  {isAuthenticated && (canEdit || canDelete) && (
                    <div style={{ display: "flex", gap: "var(--space-2)" }}>
                      {canEdit && (
                        <button
                          className={styles.reactionBtn}
                          onClick={() => {
                            setEditingPostId(editingPostId === post.id ? null : post.id);
                            setEditContent(post.content);
                          }}
                        >
                          <Pencil size={14} />
                        </button>
                      )}
                      {canDelete && (
                        <button
                          className={styles.reactionBtn}
                          onClick={() => handleDeletePost(post.id)}
                          style={{ color: "var(--danger)" }}
                        >
                          <Trash2 size={14} />
                        </button>
                      )}
                    </div>
                  )}
                </div>

                {editingPostId === post.id ? (
                  <div style={{ padding: "var(--space-4) var(--space-5)" }}>
                    <textarea
                      className="input"
                      rows={4}
                      value={editContent}
                      onChange={(e) => setEditContent(e.target.value)}
                      style={{ resize: "vertical", marginBottom: "var(--space-3)" }}
                    />
                    <div style={{ display: "flex", gap: "var(--space-2)" }}>
                      <button className="btn btn-primary btn-sm" onClick={() => handleEditPost(post.id)}>Save</button>
                      <button className="btn btn-secondary btn-sm" onClick={() => setEditingPostId(null)}>Cancel</button>
                    </div>
                  </div>
                ) : (
                  <div className={styles.postBody}
                    dangerouslySetInnerHTML={{ __html: post.content
                      .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
                      .replace(/!\[([^\]]*)\]\((https?:\/\/[^)]+)\)/g, '<img src="$2" alt="$1" style="max-width:100%;border-radius:8px;margin:8px 0" />')
                      .replace(/\n/g, "<br/>")
                    }}
                  />
                )}

                <div className={styles.postActions}>
                  <button
                    className={`${styles.reactionBtn} ${post.currentUserReaction === "like" ? styles.reactionBtnActive : ""}`}
                    onClick={() => handleReact(post.id, "like")}
                  >
                    {post.currentUserReaction === "like" ? <ThumbsUp size={16} fill="currentColor" /> : <ThumbsUp size={16} />} {post.likeCount || 0}
                  </button>
                  <button
                    className={`${styles.reactionBtn} ${post.currentUserReaction === "dislike" ? styles.reactionBtnActive : ""}`}
                    onClick={() => handleReact(post.id, "dislike")}
                  >
                    {post.currentUserReaction === "dislike" ? <ThumbsDown size={16} fill="currentColor" /> : <ThumbsDown size={16} />} {post.dislikeCount || 0}
                  </button>
                </div>
              </div>
            );
          })}
        </div>

        <Pagination 
          currentPage={currentPage}
          totalCount={totalPosts}
          pageSize={20}
          baseUrl={`/forum/thread/${threadId}`}
        />

        {/* Reply */}
        {thread.isLocked ? (
          <div className={styles.lockedMessage}>
            <Lock size={16} style={{ display: "inline", marginRight: 6 }} /> This thread is locked. No new replies can be posted.
          </div>
        ) : isAuthenticated ? (
          <form onSubmit={handleReply} className={styles.replyBox}>
            <h3>Reply</h3>
            <textarea
              className="input"
              rows={4}
              value={replyContent}
              onChange={(e) => setReplyContent(e.target.value)}
              placeholder="Write your reply..."
              required
              style={{ resize: "vertical", marginBottom: "var(--space-4)" }}
            />
            <button type="submit" className="btn btn-primary btn-sm" disabled={submitting}>
              {submitting ? "Posting..." : "Post Reply"}
            </button>
          </form>
        ) : (
          <div className={styles.replyBox} style={{ textAlign: "center" }}>
            <p style={{ color: "var(--text-muted)", marginBottom: "var(--space-3)" }}>
              Sign in to reply to this thread.
            </p>
            <Link href={`/login?redirect=/forum/thread/${threadId}`} className="btn btn-primary btn-sm">
              Sign In
            </Link>
          </div>
        )}
      </div>
    </div>
  );
}
