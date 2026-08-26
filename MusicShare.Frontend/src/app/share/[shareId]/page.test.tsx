import { render, screen, cleanup } from '@testing-library/react';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';

// Mock ResultPoller component
vi.mock('../../../components/ResultPoller', () => ({
  ResultPoller: ({ shareId, initialData }: { shareId: string; initialData?: unknown }) => (
    <div data-testid="result-poller" data-share-id={shareId} data-has-initial-data={!!initialData}>
      ResultPoller Component (shareId: {shareId})
    </div>
  ),
}));

// Mock fetch globally
global.fetch = vi.fn();

import ShareResultPage, { generateMetadata } from './page';

describe('ShareResultPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Reset fetch mock
    vi.mocked(global.fetch).mockResolvedValue({
      ok: true,
      json: async () => ({
        shareId: 'test-123',
        status: 'Completed',
        song: {
          id: 'song-1',
          title: 'Test Song',
          artists: ['Test Artist'],
          status: 'Resolved',
          links: [],
        },
      }),
    } as Response);
  });

  afterEach(() => {
    cleanup();
  });

  describe('Page Structure', () => {
    it('renders the page with shareId', async () => {
      const params = Promise.resolve({ shareId: 'test-123' });
      const page = await ShareResultPage({ params });

      render(page);

      expect(screen.getByTestId('result-poller')).toBeInTheDocument();
    });

    it('has correct layout structure with gradient background', async () => {
      const params = Promise.resolve({ shareId: 'test-456' });
      const page = await ShareResultPage({ params });

      const { container } = render(page);

      // Check for outer container with gradient background
      const outerDiv = container.firstChild as HTMLElement;
      expect(outerDiv).toHaveClass('min-h-screen');
      expect(outerDiv).toHaveClass('bg-linear-to-br');
      expect(outerDiv).toHaveClass('from-purple-500');
      expect(outerDiv).toHaveClass('to-pink-500');
      expect(outerDiv).toHaveClass('flex');
      expect(outerDiv).toHaveClass('items-center');
      expect(outerDiv).toHaveClass('justify-center');
      expect(outerDiv).toHaveClass('p-4');
    });

    it('has white card container with proper styling', async () => {
      const params = Promise.resolve({ shareId: 'test-789' });
      const page = await ShareResultPage({ params });

      const { container } = render(page);

      const cardDiv = container.querySelector('.bg-white') as HTMLElement;
      expect(cardDiv).toBeInTheDocument();
      expect(cardDiv).toHaveClass('rounded-lg');
      expect(cardDiv).toHaveClass('shadow-xl');
      expect(cardDiv).toHaveClass('p-8');
      expect(cardDiv).toHaveClass('max-w-2xl');
      expect(cardDiv).toHaveClass('w-full');
    });
  });

  describe('ResultPoller Integration', () => {
    it('renders ResultPoller with correct shareId', async () => {
      const params = Promise.resolve({ shareId: 'unique-share-id' });
      const page = await ShareResultPage({ params });

      render(page);

      const poller = screen.getByTestId('result-poller');
      expect(poller).toHaveAttribute('data-share-id', 'unique-share-id');
    });

    it('passes initialData when status is Completed', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'completed-123',
          status: 'Completed',
          song: {
            id: 'song-1',
            title: 'Completed Song',
            artists: ['Artist'],
            status: 'Resolved',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'completed-123' });
      const page = await ShareResultPage({ params });

      render(page);

      const poller = screen.getByTestId('result-poller');
      expect(poller).toHaveAttribute('data-has-initial-data', 'true');
    });

    it('does not pass initialData when status is Pending', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'pending-123',
          status: 'Pending',
          song: {
            id: 'song-1',
            title: 'Pending Song',
            artists: ['Artist'],
            status: 'Pending',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'pending-123' });
      const page = await ShareResultPage({ params });

      render(page);

      const poller = screen.getByTestId('result-poller');
      expect(poller).toHaveAttribute('data-has-initial-data', 'false');
    });

    it('does not pass initialData when status is Processing', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'processing-123',
          status: 'Processing',
          song: {
            id: 'song-1',
            title: 'Processing Song',
            artists: ['Artist'],
            status: 'Pending',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'processing-123' });
      const page = await ShareResultPage({ params });

      render(page);

      const poller = screen.getByTestId('result-poller');
      expect(poller).toHaveAttribute('data-has-initial-data', 'false');
    });

    it('does not pass initialData when status is Failed', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'failed-123',
          status: 'Failed',
          song: {
            id: 'song-1',
            title: 'Failed Song',
            artists: ['Artist'],
            status: 'Failed',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'failed-123' });
      const page = await ShareResultPage({ params });

      render(page);

      const poller = screen.getByTestId('result-poller');
      expect(poller).toHaveAttribute('data-has-initial-data', 'false');
    });
  });

  describe('API Fetching', () => {
    it('fetches share result from API with correct shareId', async () => {
      const params = Promise.resolve({ shareId: 'api-test-123' });
      await ShareResultPage({ params });

      expect(global.fetch).toHaveBeenCalledWith(expect.stringContaining('/api/share/api-test-123'));
    });

    it('uses environment variable for API base URL (HTTPS)', async () => {
      const originalEnv = process.env.services__api__https__0;
      process.env.services__api__https__0 = 'https://api.example.com';

      const params = Promise.resolve({ shareId: 'env-test' });
      await ShareResultPage({ params });

      expect(global.fetch).toHaveBeenCalledWith('https://api.example.com/api/share/env-test');

      process.env.services__api__https__0 = originalEnv;
    });

    it('falls back to HTTP env var if HTTPS not available', async () => {
      const originalHttps = process.env.services__api__https__0;
      const originalHttp = process.env.services__api__http__0;

      delete process.env.services__api__https__0;
      process.env.services__api__http__0 = 'http://api.example.com';

      const params = Promise.resolve({ shareId: 'http-test' });
      await ShareResultPage({ params });

      expect(global.fetch).toHaveBeenCalledWith('http://api.example.com/api/share/http-test');

      process.env.services__api__https__0 = originalHttps;
      process.env.services__api__http__0 = originalHttp;
    });

    it('uses relative path when no env vars are set', async () => {
      const originalHttps = process.env.services__api__https__0;
      const originalHttp = process.env.services__api__http__0;

      delete process.env.services__api__https__0;
      delete process.env.services__api__http__0;

      const params = Promise.resolve({ shareId: 'relative-test' });
      await ShareResultPage({ params });

      expect(global.fetch).toHaveBeenCalledWith('./api/share/relative-test');

      process.env.services__api__https__0 = originalHttps;
      process.env.services__api__http__0 = originalHttp;
    });
  });

  describe('Edge Cases', () => {
    it('handles different shareId formats', async () => {
      const testCases = [
        'simple-123',
        'with-dashes-and-numbers-456',
        'UPPERCASE',
        'MixedCase123',
        'with_underscores',
        'very-long-share-id-with-many-segments-123-456-789',
      ];

      for (const shareId of testCases) {
        vi.clearAllMocks();
        const params = Promise.resolve({ shareId });
        const page = await ShareResultPage({ params });

        render(page);

        const poller = screen.getByTestId('result-poller');
        expect(poller).toHaveAttribute('data-share-id', shareId);
        expect(global.fetch).toHaveBeenCalledWith(expect.stringContaining(`/api/share/${shareId}`));

        // Clean up after each iteration
        cleanup();
      }
    });

    it('handles API response with missing song data', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'no-song',
          status: 'Pending',
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'no-song' });
      const page = await ShareResultPage({ params });

      render(page);

      expect(screen.getByTestId('result-poller')).toBeInTheDocument();
    });

    it('handles API response with partial song data', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'partial-song',
          status: 'Completed',
          song: {
            id: 'song-1',
            title: 'Partial Song',
            artists: ['Artist'],
            // Missing optional fields like album, artworkUrl
            status: 'Resolved',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'partial-song' });
      const page = await ShareResultPage({ params });

      render(page);

      const poller = screen.getByTestId('result-poller');
      expect(poller).toHaveAttribute('data-has-initial-data', 'true');
    });
  });

  describe('generateMetadata', () => {
    it('generates metadata with song information when data is available', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'meta-123',
          status: 'Completed',
          song: {
            id: 'song-1',
            title: 'Amazing Song',
            artists: ['Artist One', 'Artist Two'],
            album: 'Great Album',
            artworkUrl: 'https://example.com/artwork.jpg',
            status: 'Resolved',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'meta-123' });
      const metadata = await generateMetadata({ params });

      expect(metadata.title).toBe('Amazing Song - Artist One, Artist Two');
      expect(metadata.description).toBe(
        'Listen to Amazing Song from Artist One, Artist Two across multiple platforms'
      );
      expect(metadata.openGraph?.title).toBe('Amazing Song - Artist One, Artist Two');
      expect(metadata.openGraph?.description).toBe(
        'Listen to Amazing Song from Artist One, Artist Two across multiple platforms'
      );
      expect(metadata.openGraph).toMatchObject({ type: 'music.song' });
      expect(metadata.openGraph?.images).toEqual([
        {
          url: 'https://music.baileymiller.dev/api/artwork/meta-123',
          width: 300,
          height: 300,
          alt: 'Amazing Song - Artist One, Artist Two',
        },
      ]);
      expect(metadata.twitter).toMatchObject({ card: 'summary_large_image' });
      expect(metadata.twitter?.title).toBe('Amazing Song - Artist One, Artist Two');
      expect(metadata.twitter?.images).toEqual(['https://music.baileymiller.dev/api/artwork/meta-123']);
    });

    it('generates metadata without artwork when artworkUrl is not provided', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'no-art-meta',
          status: 'Completed',
          song: {
            id: 'song-1',
            title: 'Song Without Art',
            artists: ['Artist'],
            status: 'Resolved',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'no-art-meta' });
      const metadata = await generateMetadata({ params });

      expect(metadata.title).toBe('Song Without Art - Artist');
      expect(metadata.openGraph?.images).toEqual([]);
      expect(metadata.twitter?.images).toEqual([]);
    });

    it('returns default metadata when song data is missing', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'no-song-meta',
          status: 'Pending',
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'no-song-meta' });
      const metadata = await generateMetadata({ params });

      expect(metadata.title).toBe('Share Result');
      expect(metadata.description).toBeUndefined();
      expect(metadata.openGraph).toBeUndefined();
      expect(metadata.twitter).toBeUndefined();
    });

    it('returns default metadata when fetch fails', async () => {
      vi.mocked(global.fetch).mockRejectedValue(new Error('Network error'));

      const params = Promise.resolve({ shareId: 'error-meta' });
      const metadata = await generateMetadata({ params });

      expect(metadata.title).toBe('Share Result');
      expect(metadata.description).toBeUndefined();
    });

    it('uses environment variable for API base URL in metadata fetch (HTTPS)', async () => {
      const originalEnv = process.env.services__api__https__0;
      process.env.services__api__https__0 = 'https://meta-api.example.com';

      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'env-meta',
          status: 'Completed',
          song: {
            id: 'song-1',
            title: 'Test',
            artists: ['Artist'],
            status: 'Resolved',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'env-meta' });
      await generateMetadata({ params });

      expect(global.fetch).toHaveBeenCalledWith('https://meta-api.example.com/api/share/env-meta');

      process.env.services__api__https__0 = originalEnv;
    });

    it('falls back to HTTP env var in metadata fetch when HTTPS not available', async () => {
      const originalHttps = process.env.services__api__https__0;
      const originalHttp = process.env.services__api__http__0;

      delete process.env.services__api__https__0;
      process.env.services__api__http__0 = 'http://meta-http.example.com';

      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'http-meta',
          status: 'Completed',
          song: {
            id: 'song-1',
            title: 'Test',
            artists: ['Artist'],
            status: 'Resolved',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'http-meta' });
      await generateMetadata({ params });

      expect(global.fetch).toHaveBeenCalledWith('http://meta-http.example.com/api/share/http-meta');

      process.env.services__api__https__0 = originalHttps;
      process.env.services__api__http__0 = originalHttp;
    });

    it('uses relative path in metadata fetch when no env vars are set', async () => {
      const originalHttps = process.env.services__api__https__0;
      const originalHttp = process.env.services__api__http__0;

      delete process.env.services__api__https__0;
      delete process.env.services__api__http__0;

      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'relative-meta',
          status: 'Completed',
          song: {
            id: 'song-1',
            title: 'Test',
            artists: ['Artist'],
            status: 'Resolved',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'relative-meta' });
      await generateMetadata({ params });

      expect(global.fetch).toHaveBeenCalledWith('./api/share/relative-meta');

      process.env.services__api__https__0 = originalHttps;
      process.env.services__api__http__0 = originalHttp;
    });

    it('handles single artist in metadata', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'single-artist-meta',
          status: 'Completed',
          song: {
            id: 'song-1',
            title: 'Solo Track',
            artists: ['Solo Artist'],
            status: 'Resolved',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'single-artist-meta' });
      const metadata = await generateMetadata({ params });

      expect(metadata.title).toBe('Solo Track - Solo Artist');
      expect(metadata.description).toBe('Listen to Solo Track from Solo Artist across multiple platforms');
    });

    it('handles multiple artists in metadata', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => ({
          shareId: 'multi-artist-meta',
          status: 'Completed',
          song: {
            id: 'song-1',
            title: 'Collab',
            artists: ['Artist 1', 'Artist 2', 'Artist 3'],
            status: 'Resolved',
            links: [],
          },
        }),
      } as Response);

      const params = Promise.resolve({ shareId: 'multi-artist-meta' });
      const metadata = await generateMetadata({ params });

      expect(metadata.title).toBe('Collab - Artist 1, Artist 2, Artist 3');
    });

    it('returns default metadata when JSON parsing fails', async () => {
      vi.mocked(global.fetch).mockResolvedValue({
        ok: true,
        json: async () => {
          throw new Error('Invalid JSON');
        },
      } as unknown as Response);

      const params = Promise.resolve({ shareId: 'invalid-json-meta' });
      const metadata = await generateMetadata({ params });

      expect(metadata.title).toBe('Share Result');
    });
  });
});
