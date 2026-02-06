import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it, vi, afterEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Mock Next.js Link component
vi.mock('next/link', () => ({
  default: ({ children, href, className }: { children: React.ReactNode; href: string; className?: string }) => (
    <a href={href} className={className}>
      {children}
    </a>
  ),
}));

// Mock NativeShare component
vi.mock('./NativeShare', () => ({
  default: ({ title, artists }: { title: string; artists: string[] }) => (
    <button data-testid="native-share">
      Share {title} - {artists.join(', ')}
    </button>
  ),
}));

// Mock MusicServiceLink component
vi.mock('./MusicLinks', () => ({
  MusicServiceLink: ({ link }: { link: { serviceType: number; url: string } }) => (
    <div data-testid={`music-link-${link.serviceType}`}>
      Link to service {link.serviceType}: {link.url}
    </div>
  ),
}));

// Mock the API module
vi.mock('../lib/api', () => ({
  api: {
    getShareResult: vi.fn(),
  },
  MusicServiceType: {
    Spotify: 1,
    AppleMusic: 2,
    YouTubeMusic: 3,
  },
}));

import { api, type ShareResultResponse } from '../lib/api';
import { ResultPoller } from './ResultPoller';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe('ResultPoller', () => {
  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading State', () => {
    it('displays loading spinner when fetching data', () => {
      vi.mocked(api.getShareResult).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      render(<ResultPoller shareId="test-123" />, { wrapper: createWrapper() });

      expect(screen.getByText('Loading...')).toBeInTheDocument();
      const spinner = document.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });

    it('displays loading spinner with correct styling', () => {
      vi.mocked(api.getShareResult).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      render(<ResultPoller shareId="test-456" />, { wrapper: createWrapper() });

      const spinner = document.querySelector('.animate-spin');
      expect(spinner).toHaveClass('rounded-full', 'h-12', 'w-12', 'border-b-2', 'border-purple-600');
    });

    it('does not show loading state when initialData is provided', async () => {
      const initialData: ShareResultResponse = {
        shareId: 'test-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Test Song',
          artists: ['Test Artist'],
          album: 'Test Album',
          artworkUrl: 'https://example.com/art.jpg',
          isExplicit: false,
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(initialData);

      render(<ResultPoller shareId="test-123" initialData={initialData} />, {
        wrapper: createWrapper(),
      });

      expect(screen.queryByText('Loading...')).not.toBeInTheDocument();
      expect(screen.getByText('Test Song')).toBeInTheDocument();
    });
  });

  describe('Error Handling', () => {
    it('displays error message when fetch fails', async () => {
      const errorMessage = 'Failed to load share result';
      vi.mocked(api.getShareResult).mockRejectedValue(new Error(errorMessage));

      render(<ResultPoller shareId="error-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText(errorMessage)).toBeInTheDocument();
      });
    });

    it('displays error with red styling', async () => {
      vi.mocked(api.getShareResult).mockRejectedValue(new Error('Test error'));

      render(<ResultPoller shareId="error-456" />, { wrapper: createWrapper() });

      await waitFor(() => {
        const errorElement = screen.getByText('Test error').closest('div');
        expect(errorElement).toHaveClass('bg-red-50', 'border', 'border-red-200', 'text-red-700');
      });
    });

    it('shows back to home link when error occurs', async () => {
      vi.mocked(api.getShareResult).mockRejectedValue(new Error('Error'));

      render(<ResultPoller shareId="error-789" />, { wrapper: createWrapper() });

      await waitFor(() => {
        const backLink = screen.getByText('← Back to home');
        expect(backLink).toBeInTheDocument();
        expect(backLink).toHaveAttribute('href', '/');
      });
    });
  });

  describe('Completed Status', () => {
    it('displays song information when status is Completed', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'complete-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Amazing Song',
          artists: ['Great Artist', 'Featured Artist'],
          album: 'Best Album',
          artworkUrl: 'https://example.com/artwork.jpg',
          isExplicit: true,
          status: 'Resolved',
          links: [
            { serviceType: 1, url: 'https://spotify.com/track/123' },
            { serviceType: 3, url: 'https://youtube.com/watch?v=abc' },
          ],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="complete-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Amazing Song')).toBeInTheDocument();
        expect(screen.getByText('Great Artist, Featured Artist')).toBeInTheDocument();
        expect(screen.getByText('Best Album')).toBeInTheDocument();
      });
    });

    it('displays artwork image when artworkUrl is provided', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'art-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Song with Art',
          artists: ['Artist'],
          artworkUrl: 'https://example.com/art.jpg',
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="art-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        const artwork = screen.getByAltText('Song with Art') as HTMLImageElement;
        expect(artwork).toBeInTheDocument();
        expect(artwork.src).toContain('art.jpg');
        expect(artwork).toHaveClass('w-24', 'h-24', 'rounded-lg', 'shadow-md');
      });
    });

    it('does not display artwork when artworkUrl is not provided', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'no-art-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Song without Art',
          artists: ['Artist'],
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="no-art-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Song without Art')).toBeInTheDocument();
      });

      const artwork = screen.queryByAltText('Song without Art');
      expect(artwork).not.toBeInTheDocument();
    });

    it('displays explicit badge when song is explicit', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'explicit-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Explicit Song',
          artists: ['Artist'],
          isExplicit: true,
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="explicit-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        const badge = screen.getByText('E');
        expect(badge).toBeInTheDocument();
        expect(badge).toHaveClass('bg-gray-400', 'text-white', 'text-xs', 'font-bold', 'rounded');
      });
    });

    it('does not display explicit badge when song is not explicit', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'clean-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Clean Song',
          artists: ['Artist'],
          isExplicit: false,
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="clean-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Clean Song')).toBeInTheDocument();
      });

      expect(screen.queryByText('E')).not.toBeInTheDocument();
    });

    it('renders service links when available', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'links-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Song with Links',
          artists: ['Artist'],
          status: 'Resolved',
          links: [
            { serviceType: 1, url: 'https://spotify.com/track/123' },
            { serviceType: 2, url: 'https://music.apple.com/track/456' },
            { serviceType: 3, url: 'https://youtube.com/watch?v=789' },
          ],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="links-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByTestId('music-link-1')).toBeInTheDocument();
        expect(screen.getByTestId('music-link-2')).toBeInTheDocument();
        expect(screen.getByTestId('music-link-3')).toBeInTheDocument();
      });
    });

    it('shows "Listen on:" heading when completed', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'complete-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Complete Song',
          artists: ['Artist'],
          status: 'Resolved',
          links: [{ serviceType: 1, url: 'https://spotify.com/track/123' }],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="complete-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Listen on:')).toBeInTheDocument();
      });
    });

    it('shows "No links found" message when completed with no links', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'no-links-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Song with No Links',
          artists: ['Artist'],
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="no-links-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(
          screen.getByText('No links found. This song may not be available on other platforms.')
        ).toBeInTheDocument();
      });
    });

    it('renders NativeShare component with song details', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'share-test-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Shareable Song',
          artists: ['Artist One', 'Artist Two'],
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="share-test-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        const nativeShareButton = screen.getByTestId('native-share');
        expect(nativeShareButton).toHaveTextContent('Share Shareable Song - Artist One, Artist Two');
      });
    });

    it('displays "Share another song" link when completed', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'another-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Song',
          artists: ['Artist'],
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="another-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        const link = screen.getByText('← Share another song');
        expect(link).toBeInTheDocument();
        expect(link).toHaveAttribute('href', '/');
      });
    });

    it('updates document title with song information', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'title-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Title Song',
          artists: ['Title Artist', 'Featured'],
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="title-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(document.title).toBe('Title Artist, Featured - Title Song');
      });
    });
  });

  describe('Pending Status', () => {
    it('displays processing message when status is Pending', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'pending-123',
        status: 'Pending',
        song: {
          id: 'song-1',
          title: 'Pending Song',
          artists: ['Artist'],
          status: 'Pending',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="pending-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Finding song across platforms...')).toBeInTheDocument();
      });
    });

    it('shows processing spinner when status is Pending', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'pending-456',
        status: 'Pending',
        song: {
          id: 'song-1',
          title: 'Song',
          artists: ['Artist'],
          status: 'Pending',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="pending-456" />, { wrapper: createWrapper() });

      await waitFor(() => {
        const processingBox = screen.getByText('Finding song across platforms...').closest('div');
        const spinner = processingBox?.querySelector('.animate-spin');
        expect(spinner).toBeInTheDocument();
      });
    });

    it('shows "Available on:" heading when pending', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'pending-789',
        status: 'Pending',
        song: {
          id: 'song-1',
          title: 'Song',
          artists: ['Artist'],
          status: 'Pending',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="pending-789" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Available on:')).toBeInTheDocument();
      });
    });

    it('shows searching message when pending with no links', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'searching-123',
        status: 'Pending',
        song: {
          id: 'song-1',
          title: 'Song',
          artists: ['Artist'],
          status: 'Pending',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="searching-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Searching for song on music platforms...')).toBeInTheDocument();
      });
    });

    it('displays blue styling for processing message', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'blue-123',
        status: 'Pending',
        song: {
          id: 'song-1',
          title: 'Song',
          artists: ['Artist'],
          status: 'Pending',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="blue-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        const messageText = screen.getByText('Finding song across platforms...');
        const messageBox = messageText.closest('div')?.parentElement;
        expect(messageBox).toHaveClass('bg-blue-50', 'border', 'border-blue-200', 'text-blue-700');
      });
    });
  });

  describe('Processing Status', () => {
    it('displays processing message when status is Processing', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'processing-123',
        status: 'Processing',
        song: {
          id: 'song-1',
          title: 'Processing Song',
          artists: ['Artist'],
          status: 'Pending',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="processing-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Finding song across platforms...')).toBeInTheDocument();
      });
    });

    it('shows "Available on:" heading when processing', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'proc-456',
        status: 'Processing',
        song: {
          id: 'song-1',
          title: 'Song',
          artists: ['Artist'],
          status: 'Pending',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="proc-456" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Available on:')).toBeInTheDocument();
      });
    });

    it('shows partial links while processing', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'partial-123',
        status: 'Processing',
        song: {
          id: 'song-1',
          title: 'Song',
          artists: ['Artist'],
          status: 'Pending',
          links: [{ serviceType: 1, url: 'https://spotify.com/track/123' }],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="partial-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByTestId('music-link-1')).toBeInTheDocument();
        expect(screen.getByText('Finding song across platforms...')).toBeInTheDocument();
      });
    });
  });

  describe('Failed Status', () => {
    it('displays failure message when status is Failed', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'failed-123',
        status: 'Failed',
        song: {
          id: 'song-1',
          title: 'Failed Song',
          artists: ['Artist'],
          status: 'Failed',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="failed-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Failed to resolve song. Please try again.')).toBeInTheDocument();
      });
    });

    it('displays failed message with red styling', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'failed-456',
        status: 'Failed',
        song: {
          id: 'song-1',
          title: 'Song',
          artists: ['Artist'],
          status: 'Failed',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="failed-456" />, { wrapper: createWrapper() });

      await waitFor(() => {
        const failedBox = screen.getByText('Failed to resolve song. Please try again.').closest('div');
        expect(failedBox).toHaveClass('bg-red-50', 'border', 'border-red-200', 'text-red-700');
      });
    });

    it('still shows song information when failed', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'failed-789',
        status: 'Failed',
        song: {
          id: 'song-1',
          title: 'Failed Song',
          artists: ['Artist'],
          status: 'Failed',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="failed-789" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Failed Song')).toBeInTheDocument();
        expect(screen.getByText('Failed to resolve song. Please try again.')).toBeInTheDocument();
      });
    });
  });

  describe('Polling Behavior', () => {
    it('polls when status is Pending', async () => {
      let callCount = 0;
      vi.mocked(api.getShareResult).mockImplementation(async () => {
        callCount++;
        return {
          shareId: 'poll-123',
          status: 'Pending',
          song: {
            id: 'song-1',
            title: 'Song',
            artists: ['Artist'],
            status: 'Pending',
            links: [],
          },
        };
      });

      render(<ResultPoller shareId="poll-123" />, { wrapper: createWrapper() });

      // Initial fetch
      await waitFor(() => {
        expect(callCount).toBe(1);
      });

      // Should poll again after 2 seconds
      await waitFor(
        () => {
          expect(callCount).toBeGreaterThanOrEqual(2);
        },
        { timeout: 3000 }
      );
    });

    it('polls when status is Processing', async () => {
      let callCount = 0;
      vi.mocked(api.getShareResult).mockImplementation(async () => {
        callCount++;
        return {
          shareId: 'poll-456',
          status: 'Processing',
          song: {
            id: 'song-1',
            title: 'Song',
            artists: ['Artist'],
            status: 'Pending',
            links: [],
          },
        };
      });

      render(<ResultPoller shareId="poll-456" />, { wrapper: createWrapper() });

      // Initial fetch
      await waitFor(() => {
        expect(callCount).toBe(1);
      });

      // Should poll again after 2 seconds
      await waitFor(
        () => {
          expect(callCount).toBeGreaterThanOrEqual(2);
        },
        { timeout: 3000 }
      );
    });

    it(
      'stops polling when status changes to Completed',
      async () => {
        let callCount = 0;
        vi.mocked(api.getShareResult).mockImplementation(async () => {
          callCount++;
          const status = callCount < 3 ? 'Pending' : 'Completed';
          return {
            shareId: 'poll-789',
            status,
            song: {
              id: 'song-1',
              title: 'Song',
              artists: ['Artist'],
              status,
              links: [],
            },
          };
        });

        render(<ResultPoller shareId="poll-789" />, { wrapper: createWrapper() });

        // Wait for status to become Completed (should happen after 2-3 polls)
        await waitFor(
          () => {
            expect(screen.queryByText('Finding song across platforms...')).not.toBeInTheDocument();
            expect(screen.getByText('Listen on:')).toBeInTheDocument();
          },
          { timeout: 10000 }
        );

        const completedCallCount = callCount;

        // Wait 3 more seconds to ensure polling has stopped
        await new Promise((resolve) => setTimeout(resolve, 3000));

        // Should not have made additional calls
        expect(callCount).toBe(completedCallCount);
      },
      15000
    );

    it(
      'stops polling when status changes to Failed',
      async () => {
        let callCount = 0;
        vi.mocked(api.getShareResult).mockImplementation(async () => {
          callCount++;
          const status = callCount < 2 ? 'Processing' : 'Failed';
          return {
            shareId: 'poll-fail',
            status,
            song: {
              id: 'song-1',
              title: 'Song',
              artists: ['Artist'],
              status,
            links: [],
          },
        };
      });

      render(<ResultPoller shareId="poll-fail" />, { wrapper: createWrapper() });

      // Wait for status to become Failed
      await waitFor(
        () => {
          expect(screen.getByText('Failed to resolve song. Please try again.')).toBeInTheDocument();
        },
        { timeout: 5000 }
      );

      const failedCallCount = callCount;

      // Wait 3 more seconds to ensure polling has stopped
      await new Promise((resolve) => setTimeout(resolve, 3000));

      // Should not have made additional calls
      expect(callCount).toBe(failedCallCount);
    },
    15000
  );
  });

  describe('Edge Cases', () => {
    it('handles missing song data gracefully', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'no-song-123',
        status: 'Pending',
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="no-song-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Finding song across platforms...')).toBeInTheDocument();
      });
    });

    it('handles missing album gracefully', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'no-album-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Song without Album',
          artists: ['Artist'],
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="no-album-123" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Song without Album')).toBeInTheDocument();
      });

      // Album should not be rendered
      expect(screen.queryByText(/Best Album/)).not.toBeInTheDocument();
    });

    it('handles single artist correctly', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'single-artist',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Solo Song',
          artists: ['Solo Artist'],
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="single-artist" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Solo Artist')).toBeInTheDocument();
        expect(document.title).toBe('Solo Artist - Solo Song');
      });
    });

    it('handles multiple artists correctly', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'multi-artist',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Collab Song',
          artists: ['Artist 1', 'Artist 2', 'Artist 3'],
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="multi-artist" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Artist 1, Artist 2, Artist 3')).toBeInTheDocument();
        expect(document.title).toBe('Artist 1, Artist 2, Artist 3 - Collab Song');
      });
    });

    it('handles very long song titles', async () => {
      const longTitle = 'A'.repeat(200);
      const mockResponse: ShareResultResponse = {
        shareId: 'long-title',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: longTitle,
          artists: ['Artist'],
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="long-title" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText(longTitle)).toBeInTheDocument();
      });
    });

    it('handles different shareId values', async () => {
      const mockResponse: ShareResultResponse = {
        shareId: 'unique-share-id-xyz-999',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Test Song',
          artists: ['Artist'],
          status: 'Resolved',
          links: [],
        },
      };

      vi.mocked(api.getShareResult).mockResolvedValue(mockResponse);

      render(<ResultPoller shareId="unique-share-id-xyz-999" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(api.getShareResult).toHaveBeenCalledWith('unique-share-id-xyz-999');
      });
    });

    it('handles transition from Pending to Completed', async () => {
      let callCount = 0;
      vi.mocked(api.getShareResult).mockImplementation(async () => {
        callCount++;
        if (callCount === 1) {
          return {
            shareId: 'transition',
            status: 'Pending',
            song: {
              id: 'song-1',
              title: 'Song',
              artists: ['Artist'],
              status: 'Pending',
              links: [],
            },
          };
        }
        return {
          shareId: 'transition',
          status: 'Completed',
          song: {
            id: 'song-1',
            title: 'Song',
            artists: ['Artist'],
            status: 'Resolved',
            links: [{ serviceType: 1, url: 'https://spotify.com/track/123' }],
          },
        };
      });

      render(<ResultPoller shareId="transition" />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Finding song across platforms...')).toBeInTheDocument();
      });

      // Wait for transition to Completed status
      await waitFor(
        () => {
          expect(screen.queryByText('Finding song across platforms...')).not.toBeInTheDocument();
          expect(screen.getByText('Listen on:')).toBeInTheDocument();
        },
        { timeout: 5000 }
      );
    });
  });
});
