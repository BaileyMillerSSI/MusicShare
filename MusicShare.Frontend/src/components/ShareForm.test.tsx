import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useRouter, useSearchParams } from 'next/navigation';

// Mock Next.js navigation hooks
vi.mock('next/navigation', () => ({
  useRouter: vi.fn(),
  useSearchParams: vi.fn(),
}));

// Mock the API module
vi.mock('../lib/api', () => ({
  api: {
    submitShare: vi.fn(),
  },
}));

import { api } from '../lib/api';
import { ShareForm } from './ShareForm';

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe('ShareForm', () => {
  const mockPush = vi.fn();
  const mockReplace = vi.fn();
  const mockSearchParams = new Map<string, string>();

  beforeEach(() => {
    vi.mocked(useRouter).mockReturnValue({
      push: mockPush,
      replace: mockReplace,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    vi.mocked(useSearchParams).mockReturnValue({
      get: (key: string) => mockSearchParams.get(key) ?? null,
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
    } as any);

    mockSearchParams.clear();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  describe('Basic Rendering', () => {
    it('renders the form with all essential elements', () => {
      render(<ShareForm />, { wrapper: createWrapper() });

      expect(screen.getByLabelText('Paste a song URL')).toBeInTheDocument();
      expect(screen.getByPlaceholderText('https://open.spotify.com/track/...')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Share Song' })).toBeInTheDocument();
      expect(screen.getByText('Supports Spotify and YouTube Music')).toBeInTheDocument();
    });

    it('renders input with correct attributes', () => {
      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      expect(input).toHaveAttribute('type', 'url');
      expect(input).toHaveAttribute('id', 'url');
      expect(input).toBeRequired();
    });

    it('renders submit button with correct styling classes', () => {
      render(<ShareForm />, { wrapper: createWrapper() });

      const button = screen.getByRole('button', { name: 'Share Song' });
      expect(button).toHaveClass('bg-purple-600');
      expect(button).toHaveClass('hover:bg-purple-700');
    });
  });

  describe('User Input', () => {
    it('allows user to type a URL', async () => {
      const user = userEvent.setup();
      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      const testUrl = 'https://open.spotify.com/track/test123';

      await user.type(input, testUrl);

      expect(input).toHaveValue(testUrl);
    });

    it('updates input value on change', async () => {
      const user = userEvent.setup();
      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');

      await user.type(input, 'https://music.youtube.com/watch?v=test');
      expect(input).toHaveValue('https://music.youtube.com/watch?v=test');

      await user.clear(input);
      await user.type(input, 'https://open.spotify.com/track/new');
      expect(input).toHaveValue('https://open.spotify.com/track/new');
    });
  });

  describe('Form Submission', () => {
    it('calls submitShare API with the entered URL', async () => {
      const user = userEvent.setup();
      const testUrl = 'https://open.spotify.com/track/test123';
      const mockResponse = { shareId: 'share-123', status: 'Pending' };

      vi.mocked(api.submitShare).mockResolvedValue(mockResponse);

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      const submitButton = screen.getByRole('button', { name: 'Share Song' });

      await user.type(input, testUrl);
      await user.click(submitButton);

      expect(api.submitShare).toHaveBeenCalledWith(testUrl);
      expect(api.submitShare).toHaveBeenCalledTimes(1);
    });

    it('prevents default form submission behavior', async () => {
      const user = userEvent.setup();
      vi.mocked(api.submitShare).mockResolvedValue({ shareId: 'test', status: 'Pending' });

      const { container } = render(<ShareForm />, { wrapper: createWrapper() });
      const form = container.querySelector('form');

      const submitHandler = vi.fn((e: Event) => e.preventDefault());
      form?.addEventListener('submit', submitHandler);

      const input = screen.getByLabelText('Paste a song URL');
      await user.type(input, 'https://open.spotify.com/track/test');
      await user.click(screen.getByRole('button', { name: 'Share Song' }));

      await waitFor(() => {
        expect(submitHandler).toHaveBeenCalled();
      });
    });

    it('does not submit if URL is empty', async () => {
      const user = userEvent.setup();
      render(<ShareForm />, { wrapper: createWrapper() });

      const submitButton = screen.getByRole('button', { name: 'Share Song' });
      await user.click(submitButton);

      expect(api.submitShare).not.toHaveBeenCalled();
    });

    it('does not submit if URL is only whitespace', async () => {
      const user = userEvent.setup();
      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      await user.type(input, '   ');

      const submitButton = screen.getByRole('button', { name: 'Share Song' });
      await user.click(submitButton);

      expect(api.submitShare).not.toHaveBeenCalled();
    });

    it('handles URLs with leading/trailing whitespace', async () => {
      const user = userEvent.setup();
      const testUrl = 'https://open.spotify.com/track/test';
      vi.mocked(api.submitShare).mockResolvedValue({ shareId: 'test', status: 'Pending' });

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL') as HTMLInputElement;

      // Paste is more realistic for whitespace handling than typing
      await user.click(input);
      await user.paste(`  ${testUrl}  `);

      // Browser's type="url" input automatically trims whitespace
      expect(input.value).toBe(testUrl);

      await user.click(screen.getByRole('button', { name: 'Share Song' }));

      await waitFor(() => {
        expect(api.submitShare).toHaveBeenCalledWith(testUrl);
      });
    });
  });

  describe('Loading State', () => {
    it('disables input and button during submission', async () => {
      const user = userEvent.setup();
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      let resolveSubmit: (value: any) => void;
      const submitPromise = new Promise((resolve) => {
        resolveSubmit = resolve;
      });

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      vi.mocked(api.submitShare).mockReturnValue(submitPromise as any);

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      const submitButton = screen.getByRole('button', { name: 'Share Song' });

      await user.type(input, 'https://open.spotify.com/track/test');
      await user.click(submitButton);

      await waitFor(() => {
        expect(input).toBeDisabled();
        expect(submitButton).toBeDisabled();
        expect(submitButton).toHaveTextContent('Processing...');
      });

      resolveSubmit!({ shareId: 'test', status: 'Pending' });

      await waitFor(() => {
        expect(input).not.toBeDisabled();
      });
    });

    it('shows "Processing..." text on submit button during loading', async () => {
      const user = userEvent.setup();
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      let resolveSubmit: (value: any) => void;
      const submitPromise = new Promise((resolve) => {
        resolveSubmit = resolve;
      });

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      vi.mocked(api.submitShare).mockReturnValue(submitPromise as any);

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      await user.type(input, 'https://open.spotify.com/track/test');
      await user.click(screen.getByRole('button', { name: 'Share Song' }));

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Processing...' })).toBeInTheDocument();
      });

      resolveSubmit!({ shareId: 'test', status: 'Pending' });
    });

    it('button has disabled styling during loading', async () => {
      const user = userEvent.setup();
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      let resolveSubmit: (value: any) => void;
      const submitPromise = new Promise((resolve) => {
        resolveSubmit = resolve;
      });

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      vi.mocked(api.submitShare).mockReturnValue(submitPromise as any);

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      const submitButton = screen.getByRole('button', { name: 'Share Song' });

      await user.type(input, 'https://open.spotify.com/track/test');
      await user.click(submitButton);

      await waitFor(() => {
        expect(submitButton).toHaveClass('disabled:opacity-50');
        expect(submitButton).toHaveClass('disabled:cursor-not-allowed');
      });

      resolveSubmit!({ shareId: 'test', status: 'Pending' });
    });
  });

  describe('Success Handling', () => {
    it('navigates to share result page on success', async () => {
      const user = userEvent.setup();
      const mockResponse = { shareId: 'share-abc-123', status: 'Pending' };
      vi.mocked(api.submitShare).mockResolvedValue(mockResponse);

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      await user.type(input, 'https://open.spotify.com/track/test');
      await user.click(screen.getByRole('button', { name: 'Share Song' }));

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/share/share-abc-123');
      });
    });

    it('navigates with different shareId values', async () => {
      const user = userEvent.setup();
      vi.mocked(api.submitShare).mockResolvedValue({ shareId: 'xyz-999', status: 'Pending' });

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      await user.type(input, 'https://music.youtube.com/watch?v=test');
      await user.click(screen.getByRole('button', { name: 'Share Song' }));

      await waitFor(() => {
        expect(mockPush).toHaveBeenCalledWith('/share/xyz-999');
      });
    });
  });

  describe('Error Handling', () => {
    it('displays error message when submission fails', async () => {
      const user = userEvent.setup();
      const errorMessage = 'Invalid URL format';
      vi.mocked(api.submitShare).mockRejectedValue(new Error(errorMessage));

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      await user.type(input, 'https://invalid-url');
      await user.click(screen.getByRole('button', { name: 'Share Song' }));

      await waitFor(() => {
        expect(screen.getByText(errorMessage)).toBeInTheDocument();
      });
    });

    it('displays error with red styling', async () => {
      const user = userEvent.setup();
      vi.mocked(api.submitShare).mockRejectedValue(new Error('Server error'));

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      await user.type(input, 'https://test.com');
      await user.click(screen.getByRole('button', { name: 'Share Song' }));

      await waitFor(() => {
        const errorElement = screen.getByText('Server error').closest('div');
        expect(errorElement).toHaveClass('bg-red-50');
        expect(errorElement).toHaveClass('border-red-200');
        expect(errorElement).toHaveClass('text-red-700');
      });
    });

    it('clears error on new submission', async () => {
      const user = userEvent.setup();
      vi.mocked(api.submitShare)
        .mockRejectedValueOnce(new Error('First error'))
        .mockResolvedValueOnce({ shareId: 'success', status: 'Pending' });

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      const submitButton = screen.getByRole('button', { name: 'Share Song' });

      // First submission fails
      await user.type(input, 'https://test1.com');
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('First error')).toBeInTheDocument();
      });

      // Second submission succeeds
      await user.clear(input);
      await user.type(input, 'https://test2.com');
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.queryByText('First error')).not.toBeInTheDocument();
      });
    });

    it('re-enables form controls after error', async () => {
      const user = userEvent.setup();
      vi.mocked(api.submitShare).mockRejectedValue(new Error('Test error'));

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      const submitButton = screen.getByRole('button', { name: 'Share Song' });

      await user.type(input, 'https://test.com');
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('Test error')).toBeInTheDocument();
      });

      expect(input).not.toBeDisabled();
      expect(submitButton).not.toBeDisabled();
      expect(submitButton).toHaveTextContent('Share Song');
    });
  });

  describe('Share Target - URL Parameter', () => {
    it('pre-fills input with URL from query parameter', () => {
      const sharedUrl = 'https://open.spotify.com/track/shared123';
      mockSearchParams.set('url', sharedUrl);

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      expect(input).toHaveValue(sharedUrl);
    });

    it('auto-submits when URL parameter is present', async () => {
      const sharedUrl = 'https://music.youtube.com/watch?v=auto123';
      mockSearchParams.set('url', sharedUrl);

      vi.mocked(api.submitShare).mockResolvedValue({ shareId: 'auto-share', status: 'Pending' });

      render(<ShareForm />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(api.submitShare).toHaveBeenCalledWith(sharedUrl);
      });
    });

    it('shows "Processing shared link..." message during auto-submit', async () => {
      mockSearchParams.set('url', 'https://test.com');

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      let resolveSubmit: (value: any) => void;
      const submitPromise = new Promise((resolve) => {
        resolveSubmit = resolve;
      });
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      vi.mocked(api.submitShare).mockReturnValue(submitPromise as any);

      render(<ShareForm />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Processing shared link...')).toBeInTheDocument();
      });

      resolveSubmit!({ shareId: 'test', status: 'Pending' });
    });

    it('shows loading indicator in auto-submit message', async () => {
      mockSearchParams.set('url', 'https://test.com');

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      let resolveSubmit: (value: any) => void;
      const submitPromise = new Promise((resolve) => {
        resolveSubmit = resolve;
      });
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      vi.mocked(api.submitShare).mockReturnValue(submitPromise as any);

      render(<ShareForm />, { wrapper: createWrapper() });

      await waitFor(() => {
        const messageContainer = screen.getByText('Processing shared link...').closest('div');
        const spinner = messageContainer?.querySelector('.animate-spin');
        expect(spinner).toBeInTheDocument();
      });

      resolveSubmit!({ shareId: 'test', status: 'Pending' });
    });

    it('clears URL parameter from browser history on auto-submit', async () => {
      mockSearchParams.set('url', 'https://test.com');

      vi.mocked(api.submitShare).mockResolvedValue({ shareId: 'test', status: 'Pending' });

      render(<ShareForm />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(mockReplace).toHaveBeenCalledWith(window.location.pathname);
      });
    });

    it('only auto-submits once even with multiple renders', async () => {
      mockSearchParams.set('url', 'https://test.com');
      vi.mocked(api.submitShare).mockResolvedValue({ shareId: 'test', status: 'Pending' });

      const { rerender } = render(<ShareForm />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(api.submitShare).toHaveBeenCalledTimes(1);
      });

      rerender(<ShareForm />);
      rerender(<ShareForm />);

      expect(api.submitShare).toHaveBeenCalledTimes(1);
    });
  });

  describe('Share Target - Text Parameter', () => {
    it('extracts URL from text parameter', () => {
      const textWithUrl = 'Check out this song: https://open.spotify.com/track/text123';
      mockSearchParams.set('text', textWithUrl);

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      expect(input).toHaveValue('https://open.spotify.com/track/text123');
    });

    it('extracts first URL from text with multiple URLs', () => {
      const textWithUrls = 'https://first.com and also https://second.com';
      mockSearchParams.set('text', textWithUrls);

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      expect(input).toHaveValue('https://first.com');
    });

    it('auto-submits when URL is extracted from text parameter', async () => {
      const textWithUrl = 'Listen to https://music.youtube.com/watch?v=text456 now!';
      mockSearchParams.set('text', textWithUrl);

      vi.mocked(api.submitShare).mockResolvedValue({ shareId: 'text-share', status: 'Pending' });

      render(<ShareForm />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(api.submitShare).toHaveBeenCalledWith('https://music.youtube.com/watch?v=text456');
      });
    });

    it('handles text parameter with http URL', () => {
      const textWithUrl = 'Check http://example.com/track/123';
      mockSearchParams.set('text', textWithUrl);

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      expect(input).toHaveValue('http://example.com/track/123');
    });

    it('handles text parameter without URL', () => {
      const plainText = 'Just some text without a URL';
      mockSearchParams.set('text', plainText);

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      expect(input).toHaveValue('');
    });

    it('prioritizes url parameter over text parameter', () => {
      mockSearchParams.set('url', 'https://from-url.com/track/1');
      mockSearchParams.set('text', 'Check https://from-text.com/track/2');

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      expect(input).toHaveValue('https://from-url.com/track/1');
    });
  });

  describe('Edge Cases', () => {
    it('handles empty URL gracefully', async () => {
      const user = userEvent.setup();
      render(<ShareForm />, { wrapper: createWrapper() });

      const submitButton = screen.getByRole('button', { name: 'Share Song' });
      await user.click(submitButton);

      expect(api.submitShare).not.toHaveBeenCalled();
    });

    it('handles very long URLs', async () => {
      const user = userEvent.setup();
      const longUrl = `https://open.spotify.com/track/${'a'.repeat(500)}`;
      vi.mocked(api.submitShare).mockResolvedValue({ shareId: 'long', status: 'Pending' });

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL') as HTMLInputElement;
      // Use paste instead of type for long strings to avoid timeout
      await user.click(input);
      await user.paste(longUrl);
      await user.click(screen.getByRole('button', { name: 'Share Song' }));

      await waitFor(() => {
        expect(api.submitShare).toHaveBeenCalledWith(longUrl);
      });
    });

    it('handles special characters in URLs', async () => {
      const user = userEvent.setup();
      const specialUrl = 'https://test.com/track?id=123&feature=true&name=test%20song';
      vi.mocked(api.submitShare).mockResolvedValue({ shareId: 'special', status: 'Pending' });

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL') as HTMLInputElement;
      // Use paste to avoid special character handling issues with userEvent.type
      await user.click(input);
      await user.paste(specialUrl);
      await user.click(screen.getByRole('button', { name: 'Share Song' }));

      await waitFor(() => {
        expect(api.submitShare).toHaveBeenCalledWith(specialUrl);
      });
    });

    it('does not show auto-submit message when manually submitting', async () => {
      const user = userEvent.setup();
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      let resolveSubmit: (value: any) => void;
      const submitPromise = new Promise((resolve) => {
        resolveSubmit = resolve;
      });
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      vi.mocked(api.submitShare).mockReturnValue(submitPromise as any);

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      await user.type(input, 'https://test.com');

      const submitButton = screen.getByRole('button', { name: 'Share Song' });
      await user.click(submitButton);

      // Wait for the button to show processing state
      await waitFor(() => {
        expect(submitButton).toHaveTextContent('Processing...');
      });

      // The auto-submit message should not be present
      expect(screen.queryByText('Processing shared link...')).not.toBeInTheDocument();

      resolveSubmit!({ shareId: 'test', status: 'Pending' });
    });

    it('handles network errors gracefully', async () => {
      const user = userEvent.setup();
      vi.mocked(api.submitShare).mockRejectedValue(new Error('Network request failed'));

      render(<ShareForm />, { wrapper: createWrapper() });

      const input = screen.getByLabelText('Paste a song URL');
      await user.type(input, 'https://test.com');
      await user.click(screen.getByRole('button', { name: 'Share Song' }));

      await waitFor(() => {
        expect(screen.getByText('Network request failed')).toBeInTheDocument();
      });
    });
  });
});
