import { apiFetch } from "./client";
import { API_BASE } from "../config";
import type {
  ApiResponse,
  PaginatedResponse,
  MusicAlbumDto,
  MusicTrackDto,
  AlbumDetailDto,
  FeaturedTracksResponse,
  FavoriteTrackDto,
  ScanTrackResponse,
  MusicPlaylist,
  CreateAlbumRequest,
  UpdateTrackRequest,
  CreatePlaylistReq,
  AddTrackToPlaylistReq,
} from "../types";

export function getStreamUrl(trackId: number): string {
  return `${API_BASE}/api/music/tracks/${trackId}/stream`;
}

export function getDownloadUrl(trackId: number): string {
  return `${API_BASE}/api/music/tracks/${trackId}/download`;
}

export const musicApi = {
  getAlbums: () =>
    apiFetch<ApiResponse<MusicAlbumDto[]>>("/api/music/albums", { auth: false }),

  getAlbumsPaged: (page: number, pageSize = 24) =>
    apiFetch<ApiResponse<PaginatedResponse<MusicAlbumDto>>>(`/api/music/albums?page=${page}&pageSize=${pageSize}`, { auth: false }),

  getAlbum: (id: number) =>
    apiFetch<ApiResponse<AlbumDetailDto>>(`/api/music/albums/${id}`, { auth: false }),

  getTracks: (params?: { albumId?: number }) => {
    const sp = new URLSearchParams();
    if (params?.albumId != null) sp.set("albumId", String(params.albumId));
    const qs = sp.toString();
    return apiFetch<ApiResponse<MusicTrackDto[]>>(`/api/music/tracks${qs ? `?${qs}` : ""}`, { auth: false });
  },

  getFeatured: () =>
    apiFetch<ApiResponse<FeaturedTracksResponse>>("/api/music/tracks/featured", { auth: false }),

  recordPlay: (trackId: number) =>
    apiFetch<ApiResponse<void>>(`/api/music/tracks/${trackId}/play`, { method: "POST", auth: false }),

  // Favorites
  getFavorites: () =>
    apiFetch<ApiResponse<FavoriteTrackDto[]>>("/api/music/favorites"),

  getFavoriteIds: () =>
    apiFetch<ApiResponse<number[]>>("/api/music/favorites/ids"),

  toggleFavorite: (trackId: number) =>
    apiFetch<ApiResponse<{ isFavorited: boolean }>>(`/api/music/favorites/${trackId}`, { method: "POST" }),

  // Playlists
  getPlaylists: () =>
    apiFetch<ApiResponse<MusicPlaylist[]>>("/api/music/playlists"),

  createPlaylist: (data: CreatePlaylistReq) =>
    apiFetch<ApiResponse<MusicPlaylist>>("/api/music/playlists", { method: "POST", body: data }),

  addTrackToPlaylist: (playlistId: string, data: AddTrackToPlaylistReq) =>
    apiFetch<ApiResponse<void>>(`/api/music/playlists/${playlistId}/tracks`, { method: "POST", body: data }),

  removeTrackFromPlaylist: (playlistId: string, trackId: number) =>
    apiFetch<ApiResponse<void>>(`/api/music/playlists/${playlistId}/tracks/${trackId}`, { method: "DELETE" }),

  // Admin: albums
  createAlbum: (data: CreateAlbumRequest) =>
    apiFetch<ApiResponse<MusicAlbumDto>>("/api/music/albums", { method: "POST", body: data }),

  updateAlbum: (id: number, data: CreateAlbumRequest) =>
    apiFetch<ApiResponse<MusicAlbumDto>>(`/api/music/albums/${id}`, { method: "PUT", body: data }),

  deleteAlbum: (id: number) =>
    apiFetch<ApiResponse<void>>(`/api/music/albums/${id}`, { method: "DELETE" }),

  uploadAlbumCover: (albumId: number, file: File) => {
    const form = new FormData();
    form.append("file", file);
    return apiFetch<ApiResponse<{ coverPath: string }>>(`/api/music/albums/${albumId}/cover`, { method: "POST", body: form });
  },

  // Admin: tracks
  scanTrack: (file: File) => {
    const form = new FormData();
    form.append("file", file);
    return apiFetch<ApiResponse<ScanTrackResponse>>("/api/music/tracks/scan", { method: "POST", body: form });
  },

  uploadTrack: (form: FormData) =>
    apiFetch<ApiResponse<MusicTrackDto>>("/api/music/tracks", { method: "POST", body: form }),

  updateTrack: (id: number, data: UpdateTrackRequest) =>
    apiFetch<ApiResponse<MusicTrackDto>>(`/api/music/tracks/${id}`, { method: "PUT", body: data }),

  deleteTrack: (id: number) =>
    apiFetch<ApiResponse<void>>(`/api/music/tracks/${id}`, { method: "DELETE" }),
};
