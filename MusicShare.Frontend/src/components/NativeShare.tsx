'use client';

import { useEffect, useState } from 'react';

interface Props {
    title: string;
    artists: string[];
    artworkUrl?: string;
}

async function loadArtworkFile(artworkUrl: string): Promise<File | undefined> {
    try {
      const response = await fetch(artworkUrl, { headers: { Accept: 'image/*' } });
      if (!response.ok) return undefined;

      const artwork = await response.blob();
      if (!artwork.type.startsWith('image/')) return undefined;

      const extension = artwork.type.split('/')[1] || 'jpeg';
      return new File([artwork], `musicshare-artwork.${extension}`, { type: artwork.type });
    } catch {
      // Sharing the URL still works when the artwork cannot be loaded.
      return undefined;
    }
}

export default function NativeShare({ title, artists, artworkUrl }: Readonly<Props>) {
    const [loadedArtwork, setLoadedArtwork] = useState<{ url: string; file?: File }>();
    const artworkReady = loadedArtwork?.url === artworkUrl;
    const artworkFile = artworkReady && loadedArtwork
      ? loadedArtwork.file
      : undefined;
    const artworkLoading = Boolean(artworkUrl && !artworkReady);

    useEffect(() => {
      let isCurrent = true;

      if (artworkUrl) {
        void loadArtworkFile(artworkUrl).then((file) => {
          if (isCurrent) setLoadedArtwork({ url: artworkUrl, file });
        });
      }

      return () => {
        isCurrent = false;
      };
    }, [artworkUrl]);

    return (<button
                disabled={artworkLoading}
                aria-busy={artworkLoading}
                onClick={async () => {
                  const shareUrl = window.location.href;
                  const shareData: ShareData = {
                    title: `${title} - ${artists.join(', ')}`,
                    url: shareUrl,
                  };

                  if (artworkFile && navigator.canShare?.({ files: [artworkFile] })) {
                    shareData.files = [artworkFile];
                  }

                  // Check if Web Share API is supported
                  if (navigator.share) {
                    try {
                      await navigator.share(shareData);
                    } catch (err) {
                      // User cancelled or error occurred
                      if ((err as Error).name !== 'AbortError') {
                        console.error('Error sharing:', err);
                      }
                    }
                  } else {
                    // Fallback: Copy to clipboard
                    try {
                      await navigator.clipboard.writeText(shareUrl);
                      alert('Link copied to clipboard!');
                    } catch (err) {
                      console.error('Error copying to clipboard:', err);
                      alert('Could not copy link. Please copy manually: ' + shareUrl);
                    }
                  }
                }}
                className="w-full flex items-center justify-center gap-2 bg-purple-600 hover:bg-purple-700 disabled:opacity-70 disabled:cursor-wait text-white py-3 px-4 rounded-lg transition-colors font-medium cursor-pointer"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8.684 13.342C8.886 12.938 9 12.482 9 12c0-.482-.114-.938-.316-1.342m0 2.684a3 3 0 110-2.684m0 2.684l6.632 3.316m-6.632-6l6.632-3.316m0 0a3 3 0 105.367-2.684 3 3 0 00-5.367 2.684zm0 9.316a3 3 0 105.368 2.684 3 3 0 00-5.368-2.684z" />
                </svg>
                Share this song
              </button>);
}
