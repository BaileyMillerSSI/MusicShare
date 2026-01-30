import { useQuery } from '@tanstack/react-query';
import { useParams, Link } from 'react-router-dom';
import { api, MusicServiceType } from '../lib/api';
import type { ServiceLink } from '../lib/api';

// Service Link Components
interface ServiceLinkProps {
  url: string;
}

const SpotifyLink: React.FC<ServiceLinkProps> = ({ url }) => (
  <a
    href={url}
    target="_blank"
    rel="noopener noreferrer"
    className="flex items-center justify-between bg-[#1DB954] hover:bg-[#1ed760] text-white py-4 px-5 rounded-lg transition-all hover:shadow-lg transform hover:-translate-y-0.5"
  >
    <div className="flex items-center gap-3">
      <svg className="w-6 h-6" viewBox="0 0 24 24" fill="currentColor">
        <path d="M12 0C5.4 0 0 5.4 0 12s5.4 12 12 12 12-5.4 12-12S18.66 0 12 0zm5.521 17.34c-.24.359-.66.48-1.021.24-2.82-1.74-6.36-2.101-10.561-1.141-.418.122-.779-.179-.899-.539-.12-.421.18-.78.54-.9 4.56-1.021 8.52-.6 11.64 1.32.42.18.479.659.301 1.02zm1.44-3.3c-.301.42-.841.6-1.262.3-3.239-1.98-8.159-2.58-11.939-1.38-.479.12-1.02-.12-1.14-.6-.12-.48.12-1.021.6-1.141C9.6 9.9 15 10.561 18.72 12.84c.361.181.54.78.241 1.2zm.12-3.36C15.24 8.4 8.82 8.16 5.16 9.301c-.6.179-1.2-.181-1.38-.721-.18-.601.18-1.2.72-1.381 4.26-1.26 11.28-1.02 15.721 1.621.539.3.719 1.02.419 1.56-.299.421-1.02.599-1.559.3z"/>
      </svg>
      <div className="flex flex-col items-start">
        <span className="font-semibold text-sm">Spotify</span>
        <span className="text-xs opacity-90">Listen now</span>
      </div>
    </div>
    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
    </svg>
  </a>
);

const AppleMusicLink: React.FC<ServiceLinkProps> = ({ url }) => (
  <a
    href={url}
    target="_blank"
    rel="noopener noreferrer"
    className="flex items-center justify-between bg-gradient-to-r from-[#FA2D48] to-[#FB5C74] hover:from-[#FB3D58] hover:to-[#FC6C84] text-white py-4 px-5 rounded-lg transition-all hover:shadow-lg transform hover:-translate-y-0.5"
  >
    <div className="flex items-center gap-3">
      <svg className="w-6 h-6" viewBox="0 0 24 24" fill="currentColor">
        <path d="M23.997 6.124c0-.738-.065-1.47-.24-2.19-.317-1.31-1.062-2.31-2.18-3.043C21.003.517 20.373.285 19.7.164c-.517-.094-1.04-.12-1.573-.13C17.362 0 16.607 0 15.852 0H8.148c-.755 0-1.51 0-2.265.034-.533.01-1.056.036-1.573.13-.673.12-1.303.353-1.877.727C1.32 1.624.575 2.624.26 3.934c-.18.72-.24 1.452-.24 2.19C0 6.884 0 7.644 0 8.405v7.19c0 .76 0 1.52.02 2.28 0 .738.06 1.47.24 2.19.315 1.31 1.06 2.31 2.178 3.043.574.374 1.204.606 1.877.727.517.094 1.04.12 1.573.13.755.034 1.51.034 2.265.034h7.704c.755 0 1.51 0 2.265-.034.533-.01 1.056-.036 1.573-.13.673-.12 1.303-.353 1.877-.727 1.118-.734 1.863-1.734 2.178-3.043.18-.72.24-1.452.24-2.19.02-.76.02-1.52.02-2.28V8.405c0-.761 0-1.521-.02-2.281zM19.093 13.8c-.003 1.54-.7 2.786-2.093 3.23-.63.198-1.308.15-1.96.154-1.03-.005-2.064-.011-3.095-.016-.124 0-.25-.017-.368-.059-.353-.12-.515-.453-.447-.822.068-.364.324-.608.702-.652.076-.008.153-.008.23-.008h4.012c.76 0 1.52.015 2.28-.004.54-.014.976-.355 1.116-.893.14-.54-.032-.986-.472-1.31-.38-.285-.827-.433-1.29-.543-.61-.143-1.225-.266-1.84-.394-1.065-.225-2.11-.51-3.11-.955-1.598-.714-2.585-1.902-2.93-3.68-.266-1.37-.048-2.67.935-3.787.81-.916 1.86-1.43 3.06-1.61 1.29-.19 2.58-.1 3.86.18 1.18.25 2.24.74 3.09 1.64.448.474.448.99-.002 1.466-.45.477-.9.476-1.348-.002-.69-.736-1.54-1.16-2.48-1.39-1.31-.32-2.62-.32-3.93.05-1.02.29-1.73.95-2.03 1.98-.33 1.12-.18 2.18.61 3.14.63.77 1.47 1.18 2.39 1.42.61.16 1.24.28 1.86.42 1.24.27 2.47.58 3.64 1.08 1.17.5 2.13 1.24 2.69 2.42.38.8.51 1.67.49 2.56z"/>
      </svg>
      <div className="flex flex-col items-start">
        <span className="font-semibold text-sm">Apple Music</span>
        <span className="text-xs opacity-90">Listen now</span>
      </div>
    </div>
    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
    </svg>
  </a>
);

const YouTubeMusicLink: React.FC<ServiceLinkProps> = ({ url }) => (
  <a
    href={url}
    target="_blank"
    rel="noopener noreferrer"
    className="flex items-center justify-between bg-[#FF0000] hover:bg-[#CC0000] text-white py-4 px-5 rounded-lg transition-all hover:shadow-lg transform hover:-translate-y-0.5"
  >
    <div className="flex items-center gap-3">
      <svg className="w-6 h-6" viewBox="0 0 24 24" fill="currentColor">
        <path d="M12 0C5.376 0 0 5.376 0 12s5.376 12 12 12 12-5.376 12-12S18.624 0 12 0zm0 19.104c-3.924 0-7.104-3.18-7.104-7.104S8.076 4.896 12 4.896s7.104 3.18 7.104 7.104-3.18 7.104-7.104 7.104zm0-13.332c-3.432 0-6.228 2.796-6.228 6.228S8.568 18.228 12 18.228s6.228-2.796 6.228-6.228S15.432 5.772 12 5.772zM9.684 15.54V8.46L15.816 12l-6.132 3.54z"/>
      </svg>
      <div className="flex flex-col items-start">
        <span className="font-semibold text-sm">YouTube Music</span>
        <span className="text-xs opacity-90">Listen now</span>
      </div>
    </div>
    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
    </svg>
  </a>
);

// Helper component to render the correct service link
const MusicServiceLink: React.FC<{ link: ServiceLink }> = ({ link }) => {
  switch (link.serviceType) {
    case MusicServiceType.Spotify:
      return <SpotifyLink url={link.url} />;
    case MusicServiceType.AppleMusic  :
      return <AppleMusicLink url={link.url} />;
    case MusicServiceType.YouTubeMusic:
      return <YouTubeMusicLink url={link.url} />;
    default:
      return null;
  }
};

export function ResultPage() {
  const { shareId } = useParams<{ shareId: string }>();

  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['share', shareId],
    queryFn: () => api.getShareResult(shareId!),
    enabled: !!shareId,
    refetchInterval: (query) => {
      // Poll every 2 seconds if status is Pending or Processing
      const data = query.state.data;
      if (data?.status === 'Pending' || data?.status === 'Processing') {
        return 2000;
      }
      return false;
    },
  });

  if (isLoading) {
    return (
      <div className="min-h-screen bg-linear-to-br from-purple-500 to-pink-500 flex items-center justify-center p-4">
        <div className="bg-white rounded-lg shadow-xl p-8 max-w-2xl w-full">
          <div className="flex items-center justify-center">
            <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-purple-600"></div>
          </div>
          <p className="text-center text-gray-600 mt-4">Loading...</p>
        </div>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="min-h-screen bg-linear-to-br from-purple-500 to-pink-500 flex items-center justify-center p-4">
        <div className="bg-white rounded-lg shadow-xl p-8 max-w-2xl w-full">
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded">
            {error.message}
          </div>
          <Link
            to="/"
            className="mt-4 inline-block text-purple-600 hover:text-purple-700"
          >
            ← Back to home
          </Link>
        </div>
      </div>
    );
  }

  const isProcessing = data?.status === 'Pending' || data?.status === 'Processing';
  const song = data?.song;

  return (
    <div className="min-h-screen bg-linear-to-br from-purple-500 to-pink-500 flex items-center justify-center p-4">
      <div className="bg-white rounded-lg shadow-xl p-8 max-w-2xl w-full">
        <Link to="/" className="inline-block text-purple-600 hover:text-purple-700 mb-6">
          ← Share another song
        </Link>

        {isProcessing && (
          <div className="bg-blue-50 border border-blue-200 text-blue-700 px-4 py-3 rounded mb-6">
            <div className="flex items-center">
              <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-blue-600 mr-3"></div>
              <span>Finding song across platforms...</span>
            </div>
          </div>
        )}

        {data?.status === 'Failed' && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded mb-6">
            Failed to resolve song. Please try again.
          </div>
        )}

        {song && (
          <div>
            <div className="flex items-start mb-6">
              {song.artworkUrl && (
                <img
                  src={song.artworkUrl}
                  alt={song.title}
                  className="w-24 h-24 rounded-lg shadow-md mr-4"
                />
              )}
              <div className="flex-1">
                <h1 className="text-2xl font-bold text-gray-800">{song.title}</h1>
                <p className="text-gray-600">{song.artists.join(', ')}</p>
                {song.album && (
                  <p className="text-sm text-gray-500 mt-1">{song.album}</p>
                )}
              </div>
            </div>

            <div className="space-y-3">
              <h2 className="text-lg font-semibold text-gray-800 mb-3">
                {isProcessing ? 'Available on:' : 'Listen on:'}
              </h2>
              {song.links.map((link) => (
                <MusicServiceLink key={link.serviceType} link={link} />
              ))}
            </div>

            {song.links.length === 0 && !isProcessing && (
              <p className="text-gray-500 text-center py-4">
                No links found. This song may not be available on other platforms.
              </p>
            )}

            {song.links.length === 0 && isProcessing && (
              <p className="text-gray-500 text-center py-4 italic">
                Searching for song on music platforms...
              </p>
            )}

            <div className="mt-6 pt-6 border-t border-gray-200">
              <button
                onClick={async () => {
                  const shareUrl = window.location.href;
                  const shareData = {
                    title: `${song.title} - ${song.artists.join(', ')}`,
                    text: `Check out "${song.title}" by ${song.artists.join(', ')} on your favorite music platform!`,
                    url: shareUrl,
                  };

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
                className="w-full flex items-center justify-center gap-2 bg-purple-600 hover:bg-purple-700 text-white py-3 px-4 rounded-lg transition-colors font-medium cursor-pointer"
              >
                <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8.684 13.342C8.886 12.938 9 12.482 9 12c0-.482-.114-.938-.316-1.342m0 2.684a3 3 0 110-2.684m0 2.684l6.632 3.316m-6.632-6l6.632-3.316m0 0a3 3 0 105.367-2.684 3 3 0 00-5.367 2.684zm0 9.316a3 3 0 105.368 2.684 3 3 0 00-5.368-2.684z" />
                </svg>
                Share this song
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
