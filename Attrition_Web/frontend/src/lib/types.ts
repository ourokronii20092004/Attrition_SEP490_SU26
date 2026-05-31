// ─── Envelope types ───────────────────────────────────────────────────────────

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  error: string | null;
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// ─── Identity Service ─────────────────────────────────────────────────────────

export interface UserDto {
  id: string;
  username: string;
  email: string | null;
  displayName: string | null;
  role: "Admin" | "User";
  avatarUrl: string | null;
  backgroundUrl: string | null;
  themeMode: string;
  themeAccent: string;
  bio: string | null;
  authProvider: string;
  joinedAt: string;
  postCount: number;
  contributionCount: number;
  mustChangePassword: boolean;
  isEmailVerified: boolean;
  pendingEmail: string | null;
  notifyOnReply: boolean;
  notifyOnMention: boolean;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  user: UserDto;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface RegisterRequest {
  username: string;
  password: string;
  email?: string;
}

export interface GoogleAuthRequest {
  code: string;
  redirectUri: string;
}

export interface RefreshRequest {
  refreshToken: string;
}

export interface VerifyEmailRequest {
  token: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

export interface UpdateProfileRequest {
  bio?: string;
  email?: string;
  notifyOnReply?: boolean;
  notifyOnMention?: boolean;
  displayName?: string;
}

export interface UpdateThemeRequest {
  themeMode: string;
  themeAccent: string;
}

export interface UpdateEmailRequest {
  newEmail: string;
  currentPassword: string;
}

export interface SetPasswordRequest {
  newPassword: string;
}

export interface UserListItem {
  id: string;
  username: string;
  role: "Admin" | "User";
  isBanned: boolean;
  joinedAt: string;
}

export interface UserSummaryDto {
  id: string;
  username: string;
  displayName: string | null;
  avatarUrl: string | null;
  role: string;
}

// ─── Enemy Service ────────────────────────────────────────────────────────────

export interface LootEntryDto {
  itemName: string;
  rarity: string;
  iconKey: string | null;
  dropChance: number;
  minQty: number;
  maxQty: number;
}

export interface EnemyResponse {
  enemyId: string;
  name: string;
  tier: string;
  spawnBiome: string | null;
  hp: number;
  ad: number;
  ap: number;
  def: number;
  res: number;
  attackSpeed: number;
  isRanged: boolean;
  expReward: number;
  goldReward: number;
  lore: string | null;
  createdAt: string;
  updatedAt: string;
  lootTable: LootEntryDto[];
}

export interface EnemyCreateRequest {
  enemyId: string;
  name: string;
  tier: string;
  spawnBiome?: string;
  hp: number;
  ad: number;
  ap: number;
  def: number;
  res: number;
  attackSpeed: number;
  isRanged: boolean;
  expReward: number;
  goldReward: number;
  lore?: string;
  lootTable?: LootEntryDto[];
}

export interface EnemyUpdateRequest {
  name: string;
  tier: string;
  spawnBiome?: string;
  hp: number;
  ad: number;
  ap: number;
  def: number;
  res: number;
  attackSpeed: number;
  isRanged: boolean;
  expReward: number;
  goldReward: number;
  lore?: string;
  lootTable?: LootEntryDto[];
}

// ─── Wiki Service ─────────────────────────────────────────────────────────────

export interface WikiCategoryDto {
  id: number;
  name: string;
  slug: string;
  description: string;
  iconUrl: string | null;
  articleCount: number;
}

export interface WikiArticleListDto {
  id: string;
  title: string;
  slug: string;
  categorySlug: string;
  authorId: string | null;
  authorName: string | null;
  updatedAt: string;
}

export interface WikiArticleDto {
  id: string;
  title: string;
  slug: string;
  categorySlug: string;
  content: string;
  authorId: string | null;
  authorName: string | null;
  lastEditorName: string | null;
  status: string;
  createdAt: string;
  updatedAt: string;
}

export interface WikiRevisionDto {
  id: string;
  articleId: string;
  content: string;
  editedByName: string | null;
  editedAt: string;
  changeNote: string | null;
}

export interface WikiContributionDto {
  id: string;
  articleId: string;
  articleTitle: string;
  contributorName: string;
  suggestedContent: string;
  changeNote: string | null;
  status: "Pending" | "Approved" | "Rejected";
  submittedAt: string;
}

export interface CreateArticleRequest {
  title: string;
  categoryId: number;
  content: string;
  status: string;
}

export interface UpdateArticleRequest {
  title?: string;
  content?: string;
  status?: string;
  changeNote?: string;
}

export interface SuggestEditRequest {
  suggestedContent: string;
  changeNote?: string;
}

export interface ReviewContributionRequest {
  status: string;
}

export interface WikiCategoryRequest {
  name: string;
  description?: string;
  iconUrl?: string;
}

// ─── Forum Service ────────────────────────────────────────────────────────────

export interface ForumCategoryDto {
  id: number;
  name: string;
  slug: string;
  description: string;
  threadCount: number;
  latestActivity: string | null;
}

export interface ForumThreadListDto {
  id: string;
  title: string;
  authorId: string;
  authorName: string;
  authorAvatar: string | null;
  isPinned: boolean;
  isLocked: boolean;
  replyCount: number;
  createdAt: string;
  lastReplyAt: string;
}

export interface ForumThreadDto {
  id: string;
  title: string;
  categorySlug: string;
  authorId: string;
  authorName: string;
  isPinned: boolean;
  isLocked: boolean;
  replyCount: number;
  createdAt: string;
}

export interface ForumPostDto {
  id: string;
  threadId: string;
  authorId: string;
  authorName: string;
  authorAvatar: string | null;
  authorRole: "Admin" | "User";
  content: string;
  createdAt: string;
  updatedAt: string | null;
  likeCount: number;
  dislikeCount: number;
  currentUserReaction: string | null;
}

export interface CreateThreadRequest {
  categoryId: number;
  title: string;
  content: string;
}

export interface CreatePostRequest {
  content: string;
}

export interface UpdatePostRequest {
  content: string;
}

export interface ReactRequest {
  reactionType: string;
}

export interface ReportPostReq {
  reason: string;
}

export interface ForumCategoryRequest {
  name: string;
  description?: string;
}

export interface AdminForumThreadDto {
  id: string;
  title: string;
  isPinned: boolean;
  isLocked: boolean;
  replyCount: number;
  createdAt: string;
  lastReplyAt: string;
  authorName: string | null;
}

export interface AdminForumPostDto {
  id: string;
  threadId: string;
  content: string;
  createdAt: string;
  updatedAt: string | null;
  isRemoved: boolean;
  removedReason: string | null;
  removedAt: string | null;
  authorName: string | null;
  removedByName: string | null;
}

export interface AdminPostReportDto {
  id: string;
  postId: string;
  postContent: string;
  authorName: string;
  reporterName: string;
  reason: string;
  status: string;
  createdAt: string;
}

export interface RemovePostRequest {
  reason: string;
}

// ─── Assets Service ───────────────────────────────────────────────────────────

export interface AssetDto {
  id: string;
  fileName: string;
  filePath: string;
  assetType: string;
  mimeType: string;
  fileSize: number;
  title: string | null;
  description: string | null;
  tags: string | null;
  uploadedBy: string | null;
  uploadedAt: string;
  updatedAt: string | null;
}

export interface UpdateAssetReq {
  title?: string;
  description?: string;
  tags?: string;
  assetType?: string;
}

// ─── Music Service ────────────────────────────────────────────────────────────

export interface MusicAlbumDto {
  albumId: number;
  title: string;
  slug: string;
  artists: string[];
  description: string | null;
  coverPath: string | null;
  isCoverUserDefined: boolean;
  releaseDate: string | null;
  albumType: string;
  genre: string | null;
  trackCount: number;
  totalDuration: number;
  createdAt: string;
}

export interface MusicTrackDto {
  trackId: number;
  albumId: number;
  title: string;
  slug: string;
  trackNumber: number;
  artists: string[];
  duration: number;
  genre: string | null;
  coverPath: string | null;
  playCount: number;
  isFeatured: boolean;
  fileSize: number;
  albumTitle: string | null;
  albumCoverPath: string | null;
}

export interface AlbumDetailDto {
  albumId: number;
  title: string;
  slug: string;
  artists: string[];
  description: string | null;
  coverPath: string | null;
  isCoverUserDefined: boolean;
  releaseDate: string | null;
  albumType: string;
  genre: string | null;
  trackCount: number;
  totalDuration: number;
  createdAt: string;
  tracks: MusicTrackDto[];
}

export interface NewestAlbumDto {
  albumId: number;
  title: string;
  coverPath: string | null;
  artists: string[];
  trackCount: number;
  newestTrackAddedAt: string;
}

export interface FeaturedTracksResponse {
  featuredTracks: MusicTrackDto[];
  newestAlbums: NewestAlbumDto[];
}

export interface FavoriteTrackDto {
  trackId: number;
  albumId: number;
  title: string;
  slug: string;
  artists: string[];
  trackNumber: number;
  duration: number;
  genre: string | null;
  coverPath: string | null;
  playCount: number;
  albumTitle: string;
  albumCoverPath: string;
  favoritedAt: string;
}

export interface CreateAlbumRequest {
  title: string;
  artists?: string[];
  description?: string;
  genre?: string;
  albumType?: string;
  releaseDate?: string;
  sortOrder?: number;
}

export interface UploadTrackRequest {
  albumId?: number;
  title?: string;
  artists?: string[];
  trackNumber?: number;
  duration?: number;
  genre?: string;
  isFeatured?: boolean;
  tempFileKey?: string;
  tempCoverPath?: string;
}

export interface UpdateTrackRequest {
  title?: string;
  artists?: string[];
  trackNumber?: number;
  duration?: number;
  genre?: string;
  isFeatured?: boolean;
}

export interface ScanTrackResponse {
  tempFileKey: string;
  title: string;
  album: string | null;
  artists: string[];
  genre: string | null;
  trackNumber: number;
  duration: number;
  tempCoverPath: string | null;
}

export interface MusicPlaylist {
  playlistId: string;
  userId: string;
  title: string;
  description: string | null;
  isPublic: boolean;
  trackCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreatePlaylistReq {
  name: string;
  description?: string;
}

export interface AddTrackToPlaylistReq {
  trackId: number;
}

// ─── Search Service ───────────────────────────────────────────────────────────

export interface SearchWikiResultDto {
  id: string;
  title: string;
  slug: string;
  categorySlug: string;
}

export interface SearchUserResultDto {
  id: string;
  username: string;
  displayName: string | null;
  avatarUrl: string | null;
}

export interface SearchPostResultDto {
  id: string;
  threadId: string;
  threadTitle: string;
  snippet: string;
}

export interface SearchEnemyResultDto {
  enemyId: string;
  name: string;
  tier: string;
}

export interface GlobalSearchResponse {
  wiki: SearchWikiResultDto[];
  users: SearchUserResultDto[];
  posts: SearchPostResultDto[];
  enemies: SearchEnemyResultDto[];
  degradedSources: string[];
}

// ─── Admin Service ────────────────────────────────────────────────────────────

export interface AdminStatsDto {
  totalUsers: number | null;
  totalWikiArticles: number | null;
  pendingContributions: number | null;
  totalForumThreads: number | null;
  totalForumPosts: number | null;
  removedPosts: number | null;
  totalEnemies: number | null;
  totalAssets: number | null;
  totalMusicAlbums: number | null;
  totalMusicTracks: number | null;
  unavailableSources: string[];
}

// ─── Character Service ────────────────────────────────────────────────────────

export interface SnapshotDto {
  level: number;
  hp: number;
  maxHp: number;
  gold: number;
  isAlive: boolean;
  roomCode: string | null;
  eventType: string;
  playtimeSeconds: number;
  capturedAt: string;
}

export interface CharacterSummaryDto {
  id: string;
  ownerId: string;
  name: string;
  archetype: string;
  createdAt: string;
  updatedAt: string;
  snapshotCount: number;
  latestSnapshot: SnapshotDto | null;
}

export interface CharacterDetailDto {
  id: string;
  ownerId: string;
  name: string;
  archetype: string;
  createdAt: string;
  updatedAt: string;
  snapshots: SnapshotDto[];
}

export interface AdminCharacterDto {
  id: string;
  ownerId: string;
  ownerUsername: string | null;
  name: string;
  archetype: string;
  updatedAt: string;
  latestSnapshot: SnapshotDto | null;
}

export interface SessionStatusDto {
  userId: string;
  username: string;
  role: "Admin" | "User";
  isBanned: boolean;
}
