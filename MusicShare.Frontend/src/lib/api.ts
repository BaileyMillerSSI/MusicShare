// API client for MusicShare backend

export interface SubmitShareRequest {
  url: string;
}

export interface SubmitShareResponse {
  shareId: string;
  status: string;
}

export interface ShareResultResponse {
  shareId: string;
  status: string;
  song?: SongDetails;
}

export interface SongDetails {
  id: string;
  title: string;
  artists: string[];
  album?: string;
  artworkUrl?: string;
  duration?: string;
  isExplicit?: boolean;
  status: string;
  links: ServiceLink[];
}

export const MusicServiceType = {
  Spotify: 1,
  AppleMusic: 2,
  YouTubeMusic: 3,
};

export type MusicServiceType = typeof MusicServiceType[keyof typeof MusicServiceType];

export interface ServiceLink {
  serviceType: MusicServiceType;
  url: string;
}

export interface PublicMetricsResponse {
  totalCompletedSongs: number;
  generatedAt?: string;
  serviceCounts: PublicMetricsServiceCount[];
  recentSongs: PublicMetricsRecentSong[];
}

export interface PublicMetricsServiceCount {
  service: MusicServiceType;
  count: number;
}

export interface PublicMetricsRecentSong {
  songId: string;
  shareId: string;
  title: string;
  artists: string[];
  album?: string;
  artworkUrl?: string;
  sourceService: MusicServiceType;
  createdAt: string;
}

export const api = {
  async submitShare(url: string): Promise<SubmitShareResponse> {
    const response = await fetch(`/api/share`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ url }),
    });

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Failed to submit share' }));
      throw new Error(error.error || 'Failed to submit share');
    }

    return response.json();
  },

  async getShareResult(shareId: string): Promise<ShareResultResponse> {
    const response = await fetch(`/api/share/${shareId}`);

    if (!response.ok) {
      const error = await response.json().catch(() => ({ error: 'Failed to get share result' }));
      throw new Error(error.error || 'Failed to get share result');
    }

    return response.json();
  },

};
