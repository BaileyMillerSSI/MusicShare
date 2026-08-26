import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach, afterEach, type Mock } from 'vitest';

import NativeShare from './NativeShare';

function setWindowLocation(href: string) {
  Object.defineProperty(window, 'location', {
    value: { href },
    writable: true,
    configurable: true,
  });
}

describe('NativeShare', () => {
  const defaultProps = {
    title: 'Test Song',
    artists: ['Artist One', 'Artist Two'],
  };

  beforeEach(() => {
    setWindowLocation('https://musicshare.example.com/share/test123');

    // Mock console.error to avoid polluting test output
    vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('Basic Rendering', () => {
    it('renders the share button with correct text', () => {
      render(<NativeShare {...defaultProps} />);

      expect(screen.getByRole('button', { name: 'Share this song' })).toBeInTheDocument();
    });

    it('renders the share button with correct styling classes', () => {
      render(<NativeShare {...defaultProps} />);

      const button = screen.getByRole('button', { name: 'Share this song' });
      expect(button).toHaveClass('w-full');
      expect(button).toHaveClass('bg-purple-600');
      expect(button).toHaveClass('hover:bg-purple-700');
      expect(button).toHaveClass('text-white');
      expect(button).toHaveClass('cursor-pointer');
    });

    it('renders the share icon SVG', () => {
      const { container } = render(<NativeShare {...defaultProps} />);

      const svg = container.querySelector('svg');
      expect(svg).toBeInTheDocument();
      expect(svg).toHaveClass('w-5');
      expect(svg).toHaveClass('h-5');
    });

    it('button is accessible and has proper role', () => {
      render(<NativeShare {...defaultProps} />);

      const button = screen.getByRole('button', { name: 'Share this song' });
      expect(button).toBeVisible();
      expect(button).toBeEnabled();
    });
  });

  describe('Web Share API Support', () => {
    it('calls navigator.share with correct data when Web Share API is supported', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      const button = screen.getByRole('button', { name: 'Share this song' });
      await user.click(button);

      expect(mockShare).toHaveBeenCalledTimes(1);
      expect(mockShare).toHaveBeenCalledWith({
        title: 'Test Song - Artist One, Artist Two',
        url: 'https://musicshare.example.com/share/test123',
      });
    });

    it('formats title correctly with single artist', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare title="Solo Song" artists={['Solo Artist']} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      expect(mockShare).toHaveBeenCalledWith({
        title: 'Solo Song - Solo Artist',
        url: 'https://musicshare.example.com/share/test123',
      });
    });

    it('formats artists correctly with multiple artists', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare title="Collab Song" artists={['Artist A', 'Artist B', 'Artist C']} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      expect(mockShare).toHaveBeenCalledWith({
        title: 'Collab Song - Artist A, Artist B, Artist C',
        url: 'https://musicshare.example.com/share/test123',
      });
    });

    it('uses current window location for share URL', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      setWindowLocation('https://custom.domain.com/share/custom-id');

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      expect(mockShare).toHaveBeenCalledWith(
        expect.objectContaining({
          url: 'https://custom.domain.com/share/custom-id',
        })
      );
    });

    it('includes artwork as a shareable image file when the browser supports file sharing', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      const mockCanShare = vi.fn().mockReturnValue(true);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });
      Object.defineProperty(navigator, 'canShare', {
        value: mockCanShare,
        writable: true,
        configurable: true,
      });
      vi.spyOn(global, 'fetch').mockResolvedValue(
        new Response(new Blob(['image'], { type: 'image/jpeg' }), {
          headers: { 'content-type': 'image/jpeg' },
        })
      );

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} artworkUrl="/api/artwork/test123" />);

      await waitFor(() => expect(global.fetch).toHaveBeenCalledWith('/api/artwork/test123', {
        headers: { Accept: 'image/*' },
      }));
      await new Promise((resolve) => setTimeout(resolve, 0));
      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      expect(mockCanShare).toHaveBeenCalledWith({ files: [expect.any(File)] });
      expect(mockShare).toHaveBeenCalledWith({
        title: 'Test Song - Artist One, Artist Two',
        url: 'https://musicshare.example.com/share/test123',
        files: [expect.any(File)],
      });
    });

    it('keeps URL sharing available when artwork loading fails', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });
      vi.spyOn(global, 'fetch').mockRejectedValue(new Error('Artwork unavailable'));

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} artworkUrl="/api/artwork/test123" />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      expect(mockShare).toHaveBeenCalledWith({
        title: 'Test Song - Artist One, Artist Two',
        url: 'https://musicshare.example.com/share/test123',
      });
    });
  });

  describe('Web Share API - Success Cases', () => {
    it('does not show any error when share completes successfully', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      await waitFor(() => {
        expect(console.error).not.toHaveBeenCalled();
      });
    });

    it('can be clicked multiple times successfully', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      const button = screen.getByRole('button', { name: 'Share this song' });
      await user.click(button);
      await user.click(button);
      await user.click(button);

      expect(mockShare).toHaveBeenCalledTimes(3);
    });
  });

  describe('Web Share API - Error Handling', () => {
    it('handles AbortError silently when user cancels share', async () => {
      const abortError = new Error('Share cancelled');
      abortError.name = 'AbortError';
      const mockShare = vi.fn().mockRejectedValue(abortError);

      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      await waitFor(() => {
        expect(mockShare).toHaveBeenCalled();
      });

      // AbortError should not be logged to console
      expect(console.error).not.toHaveBeenCalled();
    });

    it('logs error to console for non-AbortError errors', async () => {
      const shareError = new Error('Share failed');
      shareError.name = 'TypeError';
      const mockShare = vi.fn().mockRejectedValue(shareError);

      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      await waitFor(() => {
        expect(console.error).toHaveBeenCalledWith('Error sharing:', shareError);
      });
    });

    it('handles generic Error objects', async () => {
      const genericError = new Error('Unknown error');
      const mockShare = vi.fn().mockRejectedValue(genericError);

      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      await waitFor(() => {
        expect(console.error).toHaveBeenCalledWith('Error sharing:', genericError);
      });
    });

    it('button remains functional after share error', async () => {
      const shareError = new Error('Share failed');
      const mockShare = vi
        .fn()
        .mockRejectedValueOnce(shareError)
        .mockResolvedValueOnce(undefined);

      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      const button = screen.getByRole('button', { name: 'Share this song' });

      // First click fails
      await user.click(button);
      await waitFor(() => {
        expect(console.error).toHaveBeenCalled();
      });

      // Second click succeeds
      vi.mocked(console.error).mockClear();
      await user.click(button);
      await waitFor(() => {
        expect(mockShare).toHaveBeenCalledTimes(2);
      });
      expect(console.error).not.toHaveBeenCalled();
    });
  });

  describe('Clipboard Fallback', () => {
    let mockWriteText: Mock<(data: string) => Promise<void>>;
    let mockAlert: Mock<typeof window.alert>;

    beforeEach(() => {
      // Remove navigator.share to simulate unsupported browser
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      delete (navigator as any).share;

      // Mock clipboard.writeText as a spy
      mockWriteText = vi.fn<(data: string) => Promise<void>>().mockResolvedValue(undefined);
      vi.spyOn(navigator.clipboard, 'writeText').mockImplementation(mockWriteText);

      // Mock window.alert
      mockAlert = vi.fn<typeof window.alert>();
      window.alert = mockAlert;
    });

    it('falls back to clipboard when Web Share API is not supported', async () => {
      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      await waitFor(() => {
        expect(mockWriteText).toHaveBeenCalledWith('https://musicshare.example.com/share/test123');
        expect(mockAlert).toHaveBeenCalledWith('Link copied to clipboard!');
      });
    });

    it('copies correct URL to clipboard', async () => {
      setWindowLocation('https://test.com/share/abc123');

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      await waitFor(() => {
        expect(mockWriteText).toHaveBeenCalledWith('https://test.com/share/abc123');
      });
    });

    it('shows success alert after clipboard copy', async () => {
      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith('Link copied to clipboard!');
      });
    });

    it('handles clipboard copy error with alert fallback', async () => {
      const clipboardError = new Error('Clipboard access denied');
      mockWriteText.mockRejectedValueOnce(clipboardError);

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      await waitFor(() => {
        expect(console.error).toHaveBeenCalledWith('Error copying to clipboard:', clipboardError);
        expect(mockAlert).toHaveBeenCalledWith(
          'Could not copy link. Please copy manually: https://musicshare.example.com/share/test123'
        );
      });
    });

    it('includes URL in manual copy alert message', async () => {
      mockWriteText.mockRejectedValueOnce(new Error('Failed'));
      setWindowLocation('https://custom.url.com/share/xyz');

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith(
          'Could not copy link. Please copy manually: https://custom.url.com/share/xyz'
        );
      });
    });

    it('can retry clipboard copy after failure', async () => {
      mockWriteText
        .mockRejectedValueOnce(new Error('Failed'))
        .mockResolvedValueOnce(undefined);

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      const button = screen.getByRole('button', { name: 'Share this song' });

      // First attempt fails
      await user.click(button);
      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith(
          expect.stringContaining('Could not copy link')
        );
      });

      // Second attempt succeeds
      mockAlert.mockClear();
      await user.click(button);
      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith('Link copied to clipboard!');
      });
    });
  });

  describe('Edge Cases', () => {
    it('handles empty artists array', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare title="Test Song" artists={[]} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      expect(mockShare).toHaveBeenCalledWith({
        title: 'Test Song - ',
        url: 'https://musicshare.example.com/share/test123',
      });
    });

    it('handles song title with special characters', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare title="Song's Title & Name (2024)" artists={['Artist']} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      expect(mockShare).toHaveBeenCalledWith({
        title: "Song's Title & Name (2024) - Artist",
        url: 'https://musicshare.example.com/share/test123',
      });
    });

    it('handles artist names with special characters', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare title="Song" artists={["Artist's Name", 'Band & Co.']} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      expect(mockShare).toHaveBeenCalledWith({
        title: "Song - Artist's Name, Band & Co.",
        url: 'https://musicshare.example.com/share/test123',
      });
    });

    it('handles very long song titles', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const longTitle = 'A'.repeat(200);
      const user = userEvent.setup();
      render(<NativeShare title={longTitle} artists={['Artist']} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      expect(mockShare).toHaveBeenCalledWith(
        expect.objectContaining({
          title: `${longTitle} - Artist`,
        })
      );
    });

    it('handles many artists', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const manyArtists = Array.from({ length: 10 }, (_, i) => `Artist ${i + 1}`);
      const user = userEvent.setup();
      render(<NativeShare title="Song" artists={manyArtists} />);

      await user.click(screen.getByRole('button', { name: 'Share this song' }));

      const expectedArtistString = manyArtists.join(', ');
      expect(mockShare).toHaveBeenCalledWith({
        title: `Song - ${expectedArtistString}`,
        url: 'https://musicshare.example.com/share/test123',
      });
    });
  });

  describe('Accessibility', () => {
    it('button is keyboard accessible', async () => {
      const mockShare = vi.fn().mockResolvedValue(undefined);
      Object.defineProperty(navigator, 'share', {
        value: mockShare,
        writable: true,
        configurable: true,
      });

      const user = userEvent.setup();
      render(<NativeShare {...defaultProps} />);

      const button = screen.getByRole('button', { name: 'Share this song' });
      button.focus();
      expect(button).toHaveFocus();

      await user.keyboard('{Enter}');
      expect(mockShare).toHaveBeenCalled();
    });

    it('button has visible cursor pointer styling', () => {
      render(<NativeShare {...defaultProps} />);

      const button = screen.getByRole('button', { name: 'Share this song' });
      expect(button).toHaveClass('cursor-pointer');
    });

    it('SVG icon is decorative and does not interfere with button label', () => {
      const { container } = render(<NativeShare {...defaultProps} />);

      const svg = container.querySelector('svg');
      expect(svg).not.toHaveAttribute('aria-label');
      expect(svg).not.toHaveAttribute('role', 'img');

      // Button text should be accessible
      expect(screen.getByRole('button', { name: 'Share this song' })).toBeInTheDocument();
    });
  });
});
