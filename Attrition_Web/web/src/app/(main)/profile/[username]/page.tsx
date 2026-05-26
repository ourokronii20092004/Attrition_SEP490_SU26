"use client";

import { useEffect, useState } from "react";
import { useParams } from "next/navigation";
import Link from "next/link";
import { api } from "@/lib/api";
import { useAuth } from "@/contexts/AuthContext";
import { formatDate, getAvatarUrl, getInitials } from "@/lib/utils";
import { Settings, MessageSquare, BookOpen, Calendar, Mail, Shield, Globe, User as UserIcon, Sword, Trash2, Edit2, Check, X } from "lucide-react";
import { useToast } from "@/contexts/ToastContext";

interface UserProfile {
  id: string;
  username: string;
  displayName: string | null;
  email: string | null;
  role: string;
  avatarPath: string | null;
  avatarUrl: string | null;
  googleAvatarUrl: string | null;
  bio: string | null;
  authProvider: string;
  joinedAt: string;
  postCount: number;
    contributionCount: number;
    backgroundUrl: string | null;
}

interface UserPost {
  id: string;
  threadId: string;
  content: string;
  createdAt: string;
  threadTitle: string;
  threadSlug: string;
}

interface UserContribution {
  id: string;
  articleId: string;
  notes: string;
  createdAt: string;
  status: string;
  articleTitle: string;
  articleSlug: string;
}

interface Character {
  characterId: string;
  characterName: string;
  characterClass: string;
  currentLevel: number;
  gold: number;
}

interface GameSave {
  saveId: string;
  characterId: string;
  saveName: string | null;
  currentScene: string;
  currentHp: number;
  maxHp: number;
  currentMana: number;
  maxMana: number;
  currentStamina: number;
  maxStamina: number;
  isAutoSave: boolean;
  createdAt: string;
}

export default function UserProfilePage() {
  const params = useParams();
  const username = params.username as string;
  const { user: currentUser } = useAuth();
  const [profile, setProfile] = useState<UserProfile | null>(null);
  const [posts, setPosts] = useState<UserPost[]>([]);
  const [contributions, setContributions] = useState<UserContribution[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(false);
  const [activeTab, setActiveTab] = useState<"posts" | "contributions" | "saves">("posts");
  const toast = useToast();

  // Characters & Saves
  const [characters, setCharacters] = useState<Character[]>([]);
  const [selectedCharId, setSelectedCharId] = useState<string>("");
  const [saves, setSaves] = useState<GameSave[]>([]);
  const [loadingSaves, setLoadingSaves] = useState(false);
  const [editingSaveId, setEditingSaveId] = useState<string | null>(null);
  const [newSaveName, setNewSaveName] = useState("");

  const [postPage, setPostPage] = useState(1);
  const [postTotal, setPostTotal] = useState(0);

  const [contribPage, setContribPage] = useState(1);
  const [contribTotal, setContribTotal] = useState(0);

  const isOwnProfile = currentUser?.username?.toLowerCase() === username.toLowerCase();

  const fetchPosts = () => {
    api.get<any>(`/users/${username}/posts?page=${postPage}&pageSize=10`).then(postsRes => {
      const pData = postsRes as any;
      if (pData.items) {
        setPosts(pData.items);
        setPostTotal(pData.totalCount);
      } else if (pData.data?.items) {
        setPosts(pData.data.items);
        setPostTotal(pData.data.totalCount);
      }
    });
  };

  const fetchContribs = () => {
    api.get<any>(`/users/${username}/contributions?page=${contribPage}&pageSize=10`).then(contribsRes => {
      const cData = contribsRes as any;
      if (cData.items) {
        setContributions(cData.items);
        setContribTotal(cData.totalCount);
      } else if (cData.data?.items) {
        setContributions(cData.data.items);
        setContribTotal(cData.data.totalCount);
      }
    });
  };

  useEffect(() => {
    api.get<UserProfile>(`/users/${username}/profile`).then((res) => {
      if (res.success && res.data) setProfile(res.data);
      else setError(true);
    }).catch(() => setError(true)).finally(() => setLoading(false));
  }, [username]);

  useEffect(() => { fetchPosts(); }, [username, postPage]);
  useEffect(() => { fetchContribs(); }, [username, contribPage]);

  useEffect(() => {
    if (isOwnProfile && activeTab === "saves") {
      api.get<Character[]>("/characters").then((res) => {
        if (res.success && res.data) {
          setCharacters(res.data);
          if (res.data.length > 0) {
            setSelectedCharId(res.data[0].characterId);
          }
        }
      });
    }
  }, [isOwnProfile, activeTab]);

  useEffect(() => {
    if (selectedCharId) {
      setLoadingSaves(true);
      api.get<GameSave[]>(`/characters/${selectedCharId}/saves`).then((res) => {
        if (res.success && res.data) {
          setSaves(res.data);
        }
      }).finally(() => setLoadingSaves(false));
    }
  }, [selectedCharId]);

  const handleRenameSave = async (saveId: string) => {
    if (!newSaveName.trim()) return;
    try {
      const res = await api.put<GameSave>(`/characters/${selectedCharId}/saves/${saveId}`, { newName: newSaveName });
      if (res.success) {
        setSaves(saves.map(s => s.saveId === saveId ? { ...s, saveName: newSaveName } : s));
        setEditingSaveId(null);
        setNewSaveName("");
        toast.success("Save renamed successfully");
      }
    } catch {
      toast.error("Failed to rename save");
    }
  };

  const handleDeleteSave = async (saveId: string) => {
    if (!confirm("Are you sure you want to delete this save? This action is irreversible.")) return;
    try {
      const res = await api.delete(`/characters/${selectedCharId}/saves/${saveId}`);
      if (res.success) {
        setSaves(saves.filter(s => s.saveId !== saveId));
        toast.success("Save deleted successfully");
      }
    } catch {
      toast.error("Failed to delete save");
    }
  };

  if (loading) {
    return (
      <div className="page">
        <div className="container" style={{ maxWidth: 720 }}>
          <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: "var(--space-4)", padding: "var(--space-12) 0" }}>
            <div className="skeleton" style={{ width: 120, height: 120, borderRadius: "50%" }} />
            <div className="skeleton skeleton-heading" style={{ width: 200 }} />
            <div className="skeleton skeleton-text" style={{ width: 300 }} />
          </div>
        </div>
      </div>
    );
  }

  if (error || !profile) {
    return (
      <div className="page">
        <div className="container">
          <div className="empty-state">
            <span className="empty-state-icon"><UserIcon size={40} /></span>
            <h3>User not found</h3>
            <p>The user &ldquo;{username}&rdquo; doesn&apos;t exist.</p>
            <Link href="/" className="btn btn-primary btn-md" style={{ marginTop: "var(--space-4)" }}>Go Home</Link>
          </div>
        </div>
      </div>
    );
  }

  const avatarUrl = getAvatarUrl(profile.avatarPath || profile.avatarUrl, profile.googleAvatarUrl);

  return (
    <div className="page">
      <div className="container" style={{ maxWidth: 720 }}>
        <div className="breadcrumb" style={{ marginBottom: "var(--space-6)" }}>
          <Link href="/">Home</Link>
          <span className="breadcrumb-separator">›</span>
          <span className="breadcrumb-current">{profile.username}</span>
        </div>

        {/* Profile Card */}
        <div style={{
          background: "var(--surface)", border: "1px solid var(--border)",
          borderRadius: "var(--radius-xl)", overflow: "hidden"
        }}>
          {/* Banner */}
          <div style={{
            height: 160,
            background: profile.backgroundUrl 
              ? `url(${getAvatarUrl(profile.backgroundUrl)}) center/cover` 
              : "linear-gradient(135deg, var(--accent) 0%, color-mix(in srgb, var(--accent), var(--bg) 40%) 100%)",
            position: "relative"
          }} />

          {/* Avatar + Actions */}
          <div style={{ padding: "0 var(--space-8)", position: "relative" }}>
            <div style={{
              display: "flex", justifyContent: "space-between", alignItems: "flex-end",
              marginTop: -48
            }}>
              <div style={{
                width: 96, height: 96, borderRadius: "50%",
                background: "var(--accent-subtle)", display: "flex", alignItems: "center",
                justifyContent: "center", fontSize: "var(--text-2xl)", fontWeight: "var(--weight-bold)",
                color: "var(--accent)", overflow: "hidden",
                border: "4px solid var(--surface)", flexShrink: 0
              }}>
                {avatarUrl ? (
                  <img src={avatarUrl} alt={profile.username} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
                ) : (
                  getInitials(profile.displayName || profile.username)
                )}
              </div>

              {isOwnProfile && (
                <div style={{ display: "flex", gap: "var(--space-2)", paddingBottom: "var(--space-2)" }}>
                  <Link href="/profile/settings" className="btn btn-secondary btn-sm" style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <Settings size={14} /> Edit Profile
                  </Link>
                </div>
              )}
            </div>
          </div>

          {/* Info */}
          <div style={{ padding: "var(--space-4) var(--space-8) var(--space-6)" }}>
            <h1 style={{ fontSize: "var(--text-xl)", marginBottom: 2 }}>
              {profile.displayName || profile.username}
            </h1>
            <p style={{ color: "var(--text-muted)", fontSize: "var(--text-sm)", marginBottom: "var(--space-3)" }}>
              @{profile.username}
            </p>

            {/* Role badge */}
            {profile.role !== "User" && (
              <span style={{
                display: "inline-flex", alignItems: "center", gap: 4,
                padding: "var(--space-1) var(--space-3)",
                background: "var(--accent-subtle)", color: "var(--accent)",
                borderRadius: "var(--radius-full)", fontSize: "var(--text-xs)",
                fontWeight: "var(--weight-semibold)", marginBottom: "var(--space-4)"
              }}>
                <Shield size={12} /> {profile.role}
              </span>
            )}

            {/* Bio */}
            {profile.bio && (
              <p style={{ color: "var(--text-secondary)", lineHeight: 1.6, marginBottom: "var(--space-5)" }}>
                {profile.bio}
              </p>
            )}

            {/* Meta info */}
            <div style={{
              display: "flex", flexWrap: "wrap", gap: "var(--space-4)",
              fontSize: "var(--text-sm)", color: "var(--text-muted)",
              marginBottom: "var(--space-5)"
            }}>
              <span style={{ display: "flex", alignItems: "center", gap: 6 }}>
                <Calendar size={14} /> Joined {formatDate(profile.joinedAt)}
              </span>
              {profile.email && (
                <span style={{ display: "flex", alignItems: "center", gap: 6 }}>
                  <Mail size={14} /> {profile.email}
                </span>
              )}
              {profile.authProvider && profile.authProvider !== "local" && (
                <span style={{ display: "flex", alignItems: "center", gap: 6 }}>
                  <Globe size={14} /> {profile.authProvider.charAt(0).toUpperCase() + profile.authProvider.slice(1)}
                </span>
              )}
            </div>

            {/* Stats */}
            <div style={{
              display: "flex", gap: "var(--space-6)",
              padding: "var(--space-5) 0",
              borderTop: "1px solid var(--border)"
            }}>
              <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)" }}>
                <MessageSquare size={16} style={{ color: "var(--accent)" }} />
                <span style={{ fontWeight: "var(--weight-bold)", fontSize: "var(--text-lg)" }}>{profile.postCount}</span>
                <span style={{ fontSize: "var(--text-sm)", color: "var(--text-muted)" }}>Posts</span>
              </div>
              <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)" }}>
                <BookOpen size={16} style={{ color: "var(--accent)" }} />
                <span style={{ fontWeight: "var(--weight-bold)", fontSize: "var(--text-lg)" }}>{profile.contributionCount}</span>
                <span style={{ fontSize: "var(--text-sm)", color: "var(--text-muted)" }}>Contributions</span>
              </div>
            </div>

          </div>

            {/* Tabs */}
            <div style={{ display: "flex", borderTop: "1px solid var(--border)", background: "var(--bg-secondary)" }}>
              <button
                className={`btn btn-sm ${activeTab === "posts" ? "" : "btn-secondary"}`}
                style={{ borderRadius: 0, borderBottom: activeTab === "posts" ? "2px solid var(--accent)" : "2px solid transparent", background: "none", marginLeft: "var(--space-4)" }}
                onClick={() => setActiveTab("posts")}
              >
                Recent Posts
              </button>
              <button
                className={`btn btn-sm ${activeTab === "contributions" ? "" : "btn-secondary"}`}
                style={{ borderRadius: 0, borderBottom: activeTab === "contributions" ? "2px solid var(--accent)" : "2px solid transparent", background: "none" }}
                onClick={() => setActiveTab("contributions")}
              >
                Recent Contributions
              </button>
              {isOwnProfile && (
                <button
                  className={`btn btn-sm ${activeTab === "saves" ? "" : "btn-secondary"}`}
                  style={{ borderRadius: 0, borderBottom: activeTab === "saves" ? "2px solid var(--accent)" : "2px solid transparent", background: "none" }}
                  onClick={() => setActiveTab("saves")}
                >
                  Game Saves
                </button>
              )}
            </div>

            {/* Tab Content */}
            <div style={{ padding: "var(--space-6)" }}>
              {activeTab === "posts" && (
                <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
                  {posts.length === 0 ? (
                    <p style={{ color: "var(--text-muted)", fontSize: "var(--text-sm)" }}>No posts found.</p>
                  ) : (
                    <>
                      {posts.map(post => (
                        <div key={post.id} style={{ padding: "var(--space-4)", background: "var(--bg-secondary)", borderRadius: "var(--radius-md)", border: "1px solid var(--border)" }}>
                          <Link href={`/forum/thread/${post.threadId}`} style={{ fontWeight: "var(--weight-semibold)", color: "var(--accent)", textDecoration: "none", display: "block", marginBottom: "var(--space-2)" }}>
                            {post.threadTitle}
                          </Link>
                          <p style={{ fontSize: "var(--text-sm)", color: "var(--text-secondary)", marginBottom: "var(--space-2)", whiteSpace: "pre-wrap", display: "-webkit-box", WebkitLineClamp: 3, WebkitBoxOrient: "vertical", overflow: "hidden" }}>
                            {post.content}
                          </p>
                          <div style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>
                            Posted {formatDate(post.createdAt)}
                          </div>
                        </div>
                      ))}
                      <div style={{ display: "flex", justifyContent: "center", gap: 8, marginTop: "var(--space-4)" }}>
                        <button className="btn btn-secondary btn-sm" disabled={postPage <= 1} onClick={() => setPostPage(p => p - 1)}>Prev</button>
                        <span style={{ fontSize: "var(--text-sm)", display: "flex", alignItems: "center" }}>Page {postPage} of {Math.ceil(postTotal / 10)}</span>
                        <button className="btn btn-secondary btn-sm" disabled={postPage >= Math.ceil(postTotal / 10)} onClick={() => setPostPage(p => p + 1)}>Next</button>
                      </div>
                    </>
                  )}
                </div>
              )}

              {activeTab === "contributions" && (
                <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
                  {contributions.length === 0 ? (
                    <p style={{ color: "var(--text-muted)", fontSize: "var(--text-sm)" }}>No contributions found.</p>
                  ) : (
                    <>
                      {contributions.map(c => (
                        <div key={c.id} style={{ padding: "var(--space-4)", background: "var(--bg-secondary)", borderRadius: "var(--radius-md)", border: "1px solid var(--border)" }}>
                          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "var(--space-2)" }}>
                            <Link href={`/wiki/category/${c.articleSlug}`} style={{ fontWeight: "var(--weight-semibold)", color: "var(--accent)", textDecoration: "none" }}>
                              {c.articleTitle}
                            </Link>
                            <span style={{ 
                              fontSize: "var(--text-xs)", padding: "2px 6px", borderRadius: "var(--radius-sm)", 
                              background: c.status === "Approved" ? "rgba(16,185,129,0.1)" : c.status === "Rejected" ? "rgba(220,38,38,0.1)" : "rgba(255,170,0,0.1)",
                              color: c.status === "Approved" ? "#10b981" : c.status === "Rejected" ? "#dc2626" : "#ffaa00"
                            }}>
                              {c.status}
                            </span>
                          </div>
                          {c.notes && (
                            <p style={{ fontSize: "var(--text-sm)", color: "var(--text-secondary)", marginBottom: "var(--space-2)" }}>
                              {c.notes}
                            </p>
                          )}
                          <div style={{ fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>
                            Submitted {formatDate(c.createdAt)}
                          </div>
                        </div>
                      ))}
                      <div style={{ display: "flex", justifyContent: "center", gap: 8, marginTop: "var(--space-4)" }}>
                        <button className="btn btn-secondary btn-sm" disabled={contribPage <= 1} onClick={() => setContribPage(p => p - 1)}>Prev</button>
                        <span style={{ fontSize: "var(--text-sm)", display: "flex", alignItems: "center" }}>Page {contribPage} of {Math.ceil(contribTotal / 10)}</span>
                        <button className="btn btn-secondary btn-sm" disabled={contribPage >= Math.ceil(contribTotal / 10)} onClick={() => setContribPage(p => p + 1)}>Next</button>
                      </div>
                    </>
                  )}
                </div>
              )}

              {activeTab === "saves" && (
                <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-5)" }}>
                  {characters.length === 0 ? (
                    <div className="empty-state" style={{ padding: "var(--space-6) 0" }}>
                      <span className="empty-state-icon"><Sword size={24} /></span>
                      <h4>No characters found</h4>
                      <p>You have not created any characters yet.</p>
                    </div>
                  ) : (
                    <>
                      <div className="input-group">
                        <label className="input-label">Select Character</label>
                        <select
                          className="input"
                          value={selectedCharId}
                          onChange={(e) => setSelectedCharId(e.target.value)}
                          style={{ maxWidth: 300 }}
                        >
                          {characters.map(char => (
                            <option key={char.characterId} value={char.characterId}>
                              {char.characterName} (Lvl {char.currentLevel} {char.characterClass})
                            </option>
                          ))}
                        </select>
                      </div>

                      {loadingSaves ? (
                        <div style={{ display: "flex", justifyContent: "center", padding: "var(--space-6) 0" }}>
                          <span className="text-muted text-sm">Loading save files...</span>
                        </div>
                      ) : saves.length === 0 ? (
                        <p style={{ color: "var(--text-muted)", fontSize: "var(--text-sm)" }}>No saves found for this character.</p>
                      ) : (
                        <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-4)" }}>
                          {saves.map(save => (
                            <div key={save.saveId} style={{ 
                              padding: "var(--space-4)", 
                              background: "var(--bg-secondary)", 
                              borderRadius: "var(--radius-md)", 
                              border: "1px solid var(--border)",
                              display: "flex",
                              justifyContent: "space-between",
                              alignItems: "center",
                              gap: "var(--space-4)"
                            }}>
                              <div style={{ flex: 1 }}>
                                <div style={{ display: "flex", alignItems: "center", gap: "var(--space-2)", marginBottom: "var(--space-2)" }}>
                                  {editingSaveId === save.saveId ? (
                                    <div style={{ display: "flex", gap: "var(--space-2)", alignItems: "center" }}>
                                      <input
                                        type="text"
                                        className="input"
                                        value={newSaveName}
                                        onChange={(e) => setNewSaveName(e.target.value)}
                                        style={{ padding: "4px 8px", fontSize: "var(--text-sm)", width: 180 }}
                                        autoFocus
                                      />
                                      <button 
                                        className="btn btn-primary btn-sm" 
                                        style={{ padding: "4px 8px" }}
                                        onClick={() => handleRenameSave(save.saveId)}
                                      >
                                        <Check size={14} />
                                      </button>
                                      <button 
                                        className="btn btn-secondary btn-sm" 
                                        style={{ padding: "4px 8px" }}
                                        onClick={() => {
                                          setEditingSaveId(null);
                                          setNewSaveName("");
                                        }}
                                      >
                                        <X size={14} />
                                      </button>
                                    </div>
                                  ) : (
                                    <>
                                      <span style={{ fontWeight: "var(--weight-semibold)" }}>
                                        {save.saveName || `Save #${save.saveId.substring(0, 8)}`}
                                      </span>
                                      <button 
                                        style={{ background: "none", border: "none", padding: 2, cursor: "pointer", color: "var(--text-muted)" }}
                                        onClick={() => {
                                          setEditingSaveId(save.saveId);
                                          setNewSaveName(save.saveName || "");
                                        }}
                                      >
                                        <Edit2 size={12} />
                                      </button>
                                    </>
                                  )}
                                  {save.isAutoSave && (
                                    <span style={{ 
                                      fontSize: "10px", 
                                      padding: "1px 5px", 
                                      borderRadius: "var(--radius-sm)", 
                                      background: "var(--accent-subtle)", 
                                      color: "var(--accent)" 
                                    }}>
                                      Auto
                                    </span>
                                  )}
                                </div>
                                
                                <div style={{ display: "flex", flexWrap: "wrap", gap: "var(--space-4)", fontSize: "var(--text-xs)", color: "var(--text-muted)" }}>
                                  <span>Scene: <strong>{save.currentScene}</strong></span>
                                  <span>HP: <strong>{save.currentHp}/{save.maxHp}</strong></span>
                                  <span>Mana: <strong>{save.currentMana}/{save.maxMana}</strong></span>
                                  <span>Stamina: <strong>{save.currentStamina}/{save.maxStamina}</strong></span>
                                  <span>Saved: <strong>{formatDate(save.createdAt)}</strong></span>
                                </div>
                              </div>

                              <button 
                                className="btn btn-secondary btn-sm" 
                                style={{ color: "var(--text-danger)", borderColor: "transparent", background: "none", padding: "var(--space-2)" }}
                                onClick={() => handleDeleteSave(save.saveId)}
                                title="Delete Save"
                              >
                                <Trash2 size={16} />
                              </button>
                            </div>
                          ))}
                        </div>
                      )}
                    </>
                  )}
                </div>
              )}
            </div>

        </div>
      </div>
    </div>
  );
}
