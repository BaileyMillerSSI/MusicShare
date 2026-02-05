'use client';

import { useState, useEffect, useRef } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useSearchParams, useRouter } from 'next/navigation';
import { api } from '../lib/api';

export function ShareForm() {
  const searchParams = useSearchParams();
  const router = useRouter();

  // Handle share target parameters - URL can come from 'url' or 'text' param
  const getSharedUrl = (): string | null => {
    const urlParam = searchParams.get('url');
    if (urlParam) return urlParam;

    // Some apps put the URL in the 'text' parameter
    const textParam = searchParams.get('text');
    if (textParam) {
      // Extract URL from text if present
      const urlMatch = textParam.match(/https?:\/\/[^\s]+/);
      if (urlMatch) return urlMatch[0];
    }

    return null;
  };

  const urlFromQuery = getSharedUrl();
  const [url, setUrl] = useState(urlFromQuery ?? '');
  const [wasAutoSubmit] = useState(!!urlFromQuery);
  const hasTriggered = useRef(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const submitMutation = useMutation({
    mutationFn: (url: string) => api.submitShare(url),
    onSuccess: (data) => {
      router.push(`/share/${data.shareId}`);
    },
  });

  useEffect(() => {
    if (wasAutoSubmit && !hasTriggered.current && !submitMutation.isPending) {
      hasTriggered.current = true;
      router.replace(window.location.pathname);
      submitMutation.mutate(url);
    }
  }, [wasAutoSubmit, submitMutation, url, router]);

  // Auto-focus input on mount for better mobile UX (skip if auto-submitting)
  useEffect(() => {
    if (!wasAutoSubmit && inputRef.current) {
      // Small delay to ensure DOM is ready and avoid iOS keyboard issues
      const timer = setTimeout(() => {
        inputRef.current?.focus();
      }, 100);
      return () => clearTimeout(timer);
    }
  }, [wasAutoSubmit]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (url.trim()) submitMutation.mutate(url);
  };

  return (
    <>
      {wasAutoSubmit && submitMutation.isPending && (
        <div className="bg-blue-50 border border-blue-200 text-blue-700 px-4 py-3 rounded mb-4">
          <div className="flex items-center">
            <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-blue-600 mr-3" />
            <span>Processing shared link...</span>
          </div>
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <label htmlFor="url" className="block text-sm font-medium text-gray-700 mb-2">
            Paste a song URL
          </label>
          <input
            ref={inputRef}
            id="url"
            type="url"
            inputMode="url"
            autoComplete="url"
            value={url}
            onChange={(e) => setUrl(e.target.value)}
            placeholder="https://open.spotify.com/track/..."
            className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-purple-500 focus:border-transparent"
            disabled={submitMutation.isPending}
            required
          />
          <p className="mt-2 text-xs text-gray-500">Supports Spotify and YouTube Music</p>
        </div>

        {submitMutation.isError && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded">
            {submitMutation.error.message}
          </div>
        )}

        <button
          type="submit"
          disabled={submitMutation.isPending}
          className="w-full bg-purple-600 text-white py-2 px-4 rounded-lg hover:bg-purple-700 focus:outline-none focus:ring-2 focus:ring-purple-500 focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
        >
          {submitMutation.isPending ? 'Processing...' : 'Share Song'}
        </button>
      </form>
    </>
  );
}
