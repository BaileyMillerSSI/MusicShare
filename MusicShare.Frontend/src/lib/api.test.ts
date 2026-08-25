import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { api } from './api';

describe('API Client', () => {
  beforeEach(() => {
    global.fetch = vi.fn();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('submitShare', () => {
    describe('Successful Submissions', () => {
      it('makes POST request to /api/share with correct headers', async () => {
        const mockResponse = { shareId: 'test-123', status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const testUrl = 'https://open.spotify.com/track/test123';
        await api.submitShare(testUrl);

        expect(fetch).toHaveBeenCalledWith('/api/share', {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({ url: testUrl }),
        });
      });

      it('returns share response with shareId and status', async () => {
        const mockResponse = { shareId: 'abc-xyz-789', status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.submitShare('https://test.com/track/1');

        expect(result).toEqual(mockResponse);
        expect(result.shareId).toBe('abc-xyz-789');
        expect(result.status).toBe('Pending');
      });

      it('handles different URL formats', async () => {
        const mockResponse = { shareId: 'test', status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const urls = [
          'https://open.spotify.com/track/test123',
          'https://music.youtube.com/watch?v=abc123',
          'https://music.apple.com/us/album/test/123',
          'http://example.com/song/456',
        ];

        for (const url of urls) {
          await api.submitShare(url);
          expect(fetch).toHaveBeenCalledWith(
            '/api/share',
            expect.objectContaining({
              body: JSON.stringify({ url }),
            })
          );
          vi.clearAllMocks();
        }
      });

      it('handles URLs with special characters', async () => {
        const mockResponse = { shareId: 'test', status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const specialUrl = 'https://test.com/track?id=123&feature=true&name=test%20song';
        await api.submitShare(specialUrl);

        expect(fetch).toHaveBeenCalledWith(
          '/api/share',
          expect.objectContaining({
            body: JSON.stringify({ url: specialUrl }),
          })
        );
      });

      it('handles very long URLs', async () => {
        const mockResponse = { shareId: 'long-url', status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const longUrl = `https://example.com/track/${'a'.repeat(500)}`;
        const result = await api.submitShare(longUrl);

        expect(result).toEqual(mockResponse);
        expect(fetch).toHaveBeenCalledWith(
          '/api/share',
          expect.objectContaining({
            body: JSON.stringify({ url: longUrl }),
          })
        );
      });
    });

    describe('Error Handling', () => {
      it('throws error when response is not ok', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          json: async () => ({ error: 'Invalid URL format' }),
        } as Response);

        await expect(api.submitShare('https://invalid.com')).rejects.toThrow('Invalid URL format');
      });

      it('throws error with custom error message from server', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          json: async () => ({ error: 'Service temporarily unavailable' }),
        } as Response);

        await expect(api.submitShare('https://test.com')).rejects.toThrow(
          'Service temporarily unavailable'
        );
      });

      it('handles non-JSON error responses', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          json: async () => {
            throw new Error('Invalid JSON');
          },
        } as unknown as Response);

        await expect(api.submitShare('https://test.com')).rejects.toThrow('Failed to submit share');
      });

      it('uses default error message when error object is empty', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          json: async () => ({}),
        } as Response);

        await expect(api.submitShare('https://test.com')).rejects.toThrow('Failed to submit share');
      });

      it('handles network errors', async () => {
        vi.mocked(fetch).mockRejectedValue(new Error('Network request failed'));

        await expect(api.submitShare('https://test.com')).rejects.toThrow('Network request failed');
      });

      it('handles timeout errors', async () => {
        vi.mocked(fetch).mockRejectedValue(new Error('Request timeout'));

        await expect(api.submitShare('https://test.com')).rejects.toThrow('Request timeout');
      });

      it('handles CORS errors', async () => {
        vi.mocked(fetch).mockRejectedValue(new Error('CORS policy blocked'));

        await expect(api.submitShare('https://test.com')).rejects.toThrow('CORS policy blocked');
      });
    });

    describe('HTTP Status Codes', () => {
      it('handles 400 Bad Request', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          status: 400,
          json: async () => ({ error: 'Invalid URL format' }),
        } as Response);

        await expect(api.submitShare('invalid')).rejects.toThrow('Invalid URL format');
      });

      it('handles 401 Unauthorized', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          status: 401,
          json: async () => ({ error: 'Unauthorized' }),
        } as Response);

        await expect(api.submitShare('https://test.com')).rejects.toThrow('Unauthorized');
      });

      it('handles 404 Not Found', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          status: 404,
          json: async () => ({ error: 'Endpoint not found' }),
        } as Response);

        await expect(api.submitShare('https://test.com')).rejects.toThrow('Endpoint not found');
      });

      it('handles 500 Internal Server Error', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          status: 500,
          json: async () => ({ error: 'Internal server error' }),
        } as Response);

        await expect(api.submitShare('https://test.com')).rejects.toThrow('Internal server error');
      });

      it('handles 503 Service Unavailable', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          status: 503,
          json: async () => ({ error: 'Service unavailable' }),
        } as Response);

        await expect(api.submitShare('https://test.com')).rejects.toThrow('Service unavailable');
      });
    });

    describe('Response Parsing', () => {
      it('correctly parses JSON response', async () => {
        const mockResponse = {
          shareId: 'parse-test-123',
          status: 'Processing',
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.submitShare('https://test.com');

        expect(result).toEqual(mockResponse);
      });

      it('handles response with additional fields', async () => {
        const mockResponse = {
          shareId: 'extra-fields-123',
          status: 'Pending',
          timestamp: '2024-01-01T00:00:00Z',
          extra: 'data',
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.submitShare('https://test.com');

        expect(result).toEqual(mockResponse);
        expect(result.shareId).toBe('extra-fields-123');
        expect(result.status).toBe('Pending');
      });
    });

    describe('Edge Cases', () => {
      it('handles empty string URL', async () => {
        const mockResponse = { shareId: 'empty', status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        await api.submitShare('');

        expect(fetch).toHaveBeenCalledWith(
          '/api/share',
          expect.objectContaining({
            body: JSON.stringify({ url: '' }),
          })
        );
      });

      it('handles URLs with unicode characters', async () => {
        const mockResponse = { shareId: 'unicode', status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const unicodeUrl = 'https://example.com/歌曲/test';
        await api.submitShare(unicodeUrl);

        expect(fetch).toHaveBeenCalledWith(
          '/api/share',
          expect.objectContaining({
            body: JSON.stringify({ url: unicodeUrl }),
          })
        );
      });

      it('handles URLs with fragments', async () => {
        const mockResponse = { shareId: 'fragment', status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const urlWithFragment = 'https://example.com/track/123#section';
        await api.submitShare(urlWithFragment);

        expect(fetch).toHaveBeenCalledWith(
          '/api/share',
          expect.objectContaining({
            body: JSON.stringify({ url: urlWithFragment }),
          })
        );
      });
    });
  });

  describe('getShareResult', () => {
    describe('Successful Fetches', () => {
      it('makes GET request to /api/share/{shareId}', async () => {
        const mockResponse = {
          shareId: 'test-123',
          status: 'Completed',
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        await api.getShareResult('test-123');

        expect(fetch).toHaveBeenCalledWith('/api/share/test-123');
      });

      it('returns share result response without song', async () => {
        const mockResponse = {
          shareId: 'pending-456',
          status: 'Processing',
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('pending-456');

        expect(result).toEqual(mockResponse);
        expect(result.shareId).toBe('pending-456');
        expect(result.status).toBe('Processing');
        expect(result.song).toBeUndefined();
      });

      it('returns share result response with complete song details', async () => {
        const mockResponse = {
          shareId: 'complete-789',
          status: 'Completed',
          song: {
            id: 'song-123',
            title: 'Test Song',
            artists: ['Artist 1', 'Artist 2'],
            album: 'Test Album',
            artworkUrl: 'https://example.com/artwork.jpg',
            duration: '3:45',
            isExplicit: false,
            status: 'Resolved',
            links: [
              {
                serviceType: 1,
                url: 'https://open.spotify.com/track/123',
              },
              {
                serviceType: 3,
                url: 'https://music.youtube.com/watch?v=456',
              },
            ],
          },
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('complete-789');

        expect(result).toEqual(mockResponse);
        expect(result.song).toBeDefined();
        expect(result.song?.title).toBe('Test Song');
        expect(result.song?.artists).toEqual(['Artist 1', 'Artist 2']);
        expect(result.song?.links).toHaveLength(2);
      });

      it('handles different shareId formats', async () => {
        const mockResponse = { shareId: 'test', status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const shareIds = [
          'simple123',
          'with-dashes-456',
          'with_underscores_789',
          'CamelCase123',
          'UPPERCASE',
          'lowercase',
          'mix3d-Ch4rs_123',
        ];

        for (const shareId of shareIds) {
          await api.getShareResult(shareId);
          expect(fetch).toHaveBeenCalledWith(`/api/share/${shareId}`);
          vi.clearAllMocks();
        }
      });

      it('handles song with partial service links', async () => {
        const mockResponse = {
          shareId: 'partial-links',
          status: 'Completed',
          song: {
            id: 'song-partial',
            title: 'Partial Song',
            artists: ['Artist'],
            status: 'PartiallyResolved',
            links: [
              {
                serviceType: 1,
                url: 'https://open.spotify.com/track/only',
              },
            ],
          },
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('partial-links');

        expect(result.song?.links).toHaveLength(1);
        expect(result.song?.status).toBe('PartiallyResolved');
      });

      it('handles song with no artwork or duration', async () => {
        const mockResponse = {
          shareId: 'minimal-song',
          status: 'Completed',
          song: {
            id: 'minimal',
            title: 'Minimal Song',
            artists: ['Artist'],
            status: 'Resolved',
            links: [],
          },
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('minimal-song');

        expect(result.song?.artworkUrl).toBeUndefined();
        expect(result.song?.duration).toBeUndefined();
        expect(result.song?.album).toBeUndefined();
        expect(result.song?.isExplicit).toBeUndefined();
      });
    });

    describe('Error Handling', () => {
      it('throws error when response is not ok', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          json: async () => ({ error: 'Share not found' }),
        } as Response);

        await expect(api.getShareResult('nonexistent')).rejects.toThrow('Share not found');
      });

      it('throws error with custom error message from server', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          json: async () => ({ error: 'Share expired' }),
        } as Response);

        await expect(api.getShareResult('expired-123')).rejects.toThrow('Share expired');
      });

      it('handles non-JSON error responses', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          json: async () => {
            throw new Error('Invalid JSON');
          },
        } as unknown as Response);

        await expect(api.getShareResult('test')).rejects.toThrow('Failed to get share result');
      });

      it('uses default error message when error object is empty', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          json: async () => ({}),
        } as Response);

        await expect(api.getShareResult('test')).rejects.toThrow('Failed to get share result');
      });

      it('handles network errors', async () => {
        vi.mocked(fetch).mockRejectedValue(new Error('Network request failed'));

        await expect(api.getShareResult('test')).rejects.toThrow('Network request failed');
      });

      it('handles timeout errors', async () => {
        vi.mocked(fetch).mockRejectedValue(new Error('Request timeout'));

        await expect(api.getShareResult('test')).rejects.toThrow('Request timeout');
      });
    });

    describe('HTTP Status Codes', () => {
      it('handles 404 Not Found', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          status: 404,
          json: async () => ({ error: 'Share not found' }),
        } as Response);

        await expect(api.getShareResult('missing')).rejects.toThrow('Share not found');
      });

      it('handles 410 Gone (expired share)', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          status: 410,
          json: async () => ({ error: 'Share has expired' }),
        } as Response);

        await expect(api.getShareResult('old-share')).rejects.toThrow('Share has expired');
      });

      it('handles 500 Internal Server Error', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          status: 500,
          json: async () => ({ error: 'Internal server error' }),
        } as Response);

        await expect(api.getShareResult('error-share')).rejects.toThrow('Internal server error');
      });

      it('handles 503 Service Unavailable', async () => {
        vi.mocked(fetch).mockResolvedValue({
          ok: false,
          status: 503,
          json: async () => ({ error: 'Service unavailable' }),
        } as Response);

        await expect(api.getShareResult('unavailable')).rejects.toThrow('Service unavailable');
      });
    });

    describe('Response Parsing', () => {
      it('correctly parses JSON response with song', async () => {
        const mockResponse = {
          shareId: 'parse-song',
          status: 'Completed',
          song: {
            id: 'song-parse',
            title: 'Parse Test',
            artists: ['Test Artist'],
            status: 'Resolved',
            links: [
              {
                serviceType: 1,
                url: 'https://spotify.com/track/1',
              },
            ],
          },
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('parse-song');

        expect(result).toEqual(mockResponse);
        expect(result.song).toBeDefined();
        expect(result.song?.title).toBe('Parse Test');
      });

      it('handles response with additional fields', async () => {
        const mockResponse = {
          shareId: 'extra',
          status: 'Completed',
          song: {
            id: 'song-extra',
            title: 'Extra Fields',
            artists: ['Artist'],
            status: 'Resolved',
            links: [],
          },
          metadata: { extra: 'data' },
          timestamp: '2024-01-01T00:00:00Z',
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('extra');

        expect(result).toEqual(mockResponse);
        expect(result.shareId).toBe('extra');
      });

      it('handles multiple artists in song', async () => {
        const mockResponse = {
          shareId: 'multi-artist',
          status: 'Completed',
          song: {
            id: 'song-multi',
            title: 'Multi Artist Song',
            artists: ['Artist 1', 'Artist 2', 'Artist 3', 'Artist 4'],
            status: 'Resolved',
            links: [],
          },
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('multi-artist');

        expect(result.song?.artists).toHaveLength(4);
        expect(result.song?.artists).toEqual(['Artist 1', 'Artist 2', 'Artist 3', 'Artist 4']);
      });

      it('handles empty artists array', async () => {
        const mockResponse = {
          shareId: 'no-artists',
          status: 'Completed',
          song: {
            id: 'song-no-artist',
            title: 'No Artist Song',
            artists: [],
            status: 'Resolved',
            links: [],
          },
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('no-artists');

        expect(result.song?.artists).toEqual([]);
      });

      it('handles all service types in links', async () => {
        const mockResponse = {
          shareId: 'all-services',
          status: 'Completed',
          song: {
            id: 'song-all',
            title: 'All Services',
            artists: ['Artist'],
            status: 'Resolved',
            links: [
              {
                serviceType: 1, // Spotify
                url: 'https://open.spotify.com/track/1',
              },
              {
                serviceType: 2, // Apple Music
                url: 'https://music.apple.com/us/album/1',
              },
              {
                serviceType: 3, // YouTube Music
                url: 'https://music.youtube.com/watch?v=1',
              },
            ],
          },
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('all-services');

        expect(result.song?.links).toHaveLength(3);
        expect(result.song?.links[0].serviceType).toBe(1);
        expect(result.song?.links[1].serviceType).toBe(2);
        expect(result.song?.links[2].serviceType).toBe(3);
      });
    });

    describe('Different Share Statuses', () => {
      it('handles Pending status', async () => {
        const mockResponse = {
          shareId: 'pending',
          status: 'Pending',
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('pending');

        expect(result.status).toBe('Pending');
        expect(result.song).toBeUndefined();
      });

      it('handles Processing status', async () => {
        const mockResponse = {
          shareId: 'processing',
          status: 'Processing',
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('processing');

        expect(result.status).toBe('Processing');
        expect(result.song).toBeUndefined();
      });

      it('handles Completed status', async () => {
        const mockResponse = {
          shareId: 'completed',
          status: 'Completed',
          song: {
            id: 'song-completed',
            title: 'Completed Song',
            artists: ['Artist'],
            status: 'Resolved',
            links: [],
          },
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('completed');

        expect(result.status).toBe('Completed');
        expect(result.song).toBeDefined();
      });

      it('handles Failed status', async () => {
        const mockResponse = {
          shareId: 'failed',
          status: 'Failed',
        };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        const result = await api.getShareResult('failed');

        expect(result.status).toBe('Failed');
        expect(result.song).toBeUndefined();
      });
    });

    describe('Edge Cases', () => {
      it('handles empty shareId', async () => {
        const mockResponse = { shareId: '', status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        await api.getShareResult('');

        expect(fetch).toHaveBeenCalledWith('/api/share/');
      });

      it('handles shareId with special characters', async () => {
        const specialId = 'test-123_ABC.def';
        const mockResponse = { shareId: specialId, status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        await api.getShareResult(specialId);

        expect(fetch).toHaveBeenCalledWith(`/api/share/${specialId}`);
      });

      it('handles very long shareId', async () => {
        const longId = 'a'.repeat(500);
        const mockResponse = { shareId: longId, status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        await api.getShareResult(longId);

        expect(fetch).toHaveBeenCalledWith(`/api/share/${longId}`);
      });

      it('handles shareId with URL-encoded characters', async () => {
        const encodedId = 'test%20space%2Fslash';
        const mockResponse = { shareId: encodedId, status: 'Pending' };
        vi.mocked(fetch).mockResolvedValue({
          ok: true,
          json: async () => mockResponse,
        } as Response);

        await api.getShareResult(encodedId);

        expect(fetch).toHaveBeenCalledWith(`/api/share/${encodedId}`);
      });
    });
  });

  describe('API URL Construction', () => {
    it('constructs correct URL for submitShare', async () => {
      vi.mocked(fetch).mockResolvedValue({
        ok: true,
        json: async () => ({ shareId: 'test', status: 'Pending' }),
      } as Response);

      await api.submitShare('https://test.com');

      const fetchCall = vi.mocked(fetch).mock.calls[0];
      expect(fetchCall[0]).toBe('/api/share');
    });

    it('constructs correct URL for getShareResult', async () => {
      vi.mocked(fetch).mockResolvedValue({
        ok: true,
        json: async () => ({ shareId: 'test-123', status: 'Pending' }),
      } as Response);

      await api.getShareResult('test-123');

      const fetchCall = vi.mocked(fetch).mock.calls[0];
      expect(fetchCall[0]).toBe('/api/share/test-123');
    });

    it('does not add trailing slash to submitShare URL', async () => {
      vi.mocked(fetch).mockResolvedValue({
        ok: true,
        json: async () => ({ shareId: 'test', status: 'Pending' }),
      } as Response);

      await api.submitShare('https://test.com');

      expect(fetch).toHaveBeenCalledWith('/api/share', expect.any(Object));
    });

    it('correctly concatenates shareId in getShareResult URL', async () => {
      vi.mocked(fetch).mockResolvedValue({
        ok: true,
        json: async () => ({ shareId: 'concat-test', status: 'Pending' }),
      } as Response);

      await api.getShareResult('concat-test');

      expect(fetch).toHaveBeenCalledWith('/api/share/concat-test');
    });
  });

  describe('Content-Type Header', () => {
    it('includes Content-Type application/json in submitShare', async () => {
      vi.mocked(fetch).mockResolvedValue({
        ok: true,
        json: async () => ({ shareId: 'test', status: 'Pending' }),
      } as Response);

      await api.submitShare('https://test.com');

      const fetchCall = vi.mocked(fetch).mock.calls[0];
      const options = fetchCall[1] as RequestInit;
      expect(options.headers).toEqual({
        'Content-Type': 'application/json',
      });
    });

    it('does not include Content-Type in getShareResult', async () => {
      vi.mocked(fetch).mockResolvedValue({
        ok: true,
        json: async () => ({ shareId: 'test', status: 'Pending' }),
      } as Response);

      await api.getShareResult('test');

      const fetchCall = vi.mocked(fetch).mock.calls[0];
      expect(fetchCall[1]).toBeUndefined();
    });
  });

  describe('HTTP Methods', () => {
    it('uses POST method for submitShare', async () => {
      vi.mocked(fetch).mockResolvedValue({
        ok: true,
        json: async () => ({ shareId: 'test', status: 'Pending' }),
      } as Response);

      await api.submitShare('https://test.com');

      const fetchCall = vi.mocked(fetch).mock.calls[0];
      const options = fetchCall[1] as RequestInit;
      expect(options.method).toBe('POST');
    });

    it('uses GET method (default) for getShareResult', async () => {
      vi.mocked(fetch).mockResolvedValue({
        ok: true,
        json: async () => ({ shareId: 'test', status: 'Pending' }),
      } as Response);

      await api.getShareResult('test');

      const fetchCall = vi.mocked(fetch).mock.calls[0];
      const options = fetchCall[1];
      // GET is the default, so options might be undefined
      expect(options?.method || 'GET').toBe('GET');
    });
  });
});
