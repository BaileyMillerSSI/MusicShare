import { useState, useEffect, useRef } from 'react';
import { useMutation } from '@tanstack/react-query';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { api } from '../lib/api';

export function SharePage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const [url, setUrl] = useState('');
  const navigate = useNavigate();
  const hasAutoSubmitted = useRef(false);

  // Get the URL from query params
  const urlFromQuery = searchParams.get('url');

  const submitMutation = useMutation({
    mutationFn: (url: string) => api.submitShare(url),
    onSuccess: (data) => {
      navigate(`/share/${data.shareId}`);
    },
  });

  // Initialize url state from query param on mount
  useEffect(() => {
    if (urlFromQuery && !url) {
      setUrl(urlFromQuery);
    }
  }, [urlFromQuery]);

  // Auto-submit when URL param is present
  useEffect(() => {
    if (
      urlFromQuery &&
      !hasAutoSubmitted.current &&
      !submitMutation.isPending
    ) {
      hasAutoSubmitted.current = true;

      // Clean URL param from address bar (prevents re-submit on refresh)
      setSearchParams({}, { replace: true });

      // Trigger the search
      submitMutation.mutate(urlFromQuery);
    }
  }, [urlFromQuery, submitMutation.isPending, setSearchParams]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (url.trim()) {
      submitMutation.mutate(url);
    }
  };

  return (
    <div className="min-h-screen bg-linear-to-br from-purple-500 to-pink-500 flex items-center justify-center p-4">
      <div className="bg-white rounded-lg shadow-xl p-8 max-w-md w-full">
        <h1 className="text-3xl font-bold text-gray-800 mb-2">Music Share</h1>
        <p className="text-gray-600 mb-6">
          Share music across platforms
        </p>

        {hasAutoSubmitted.current && submitMutation.isPending && (
          <div className="bg-blue-50 border border-blue-200 text-blue-700 px-4 py-3 rounded mb-4">
            <div className="flex items-center">
              <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-blue-600 mr-3"></div>
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
              id="url"
              type="url"
              value={url}
              onChange={(e) => setUrl(e.target.value)}
              placeholder="https://open.spotify.com/track/..."
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-purple-500 focus:border-transparent"
              disabled={submitMutation.isPending}
              required
            />
            <p className="mt-2 text-xs text-gray-500">
              Supports Spotify and YouTube Music
            </p>
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

        <div className="mt-6 text-sm text-gray-600">
          <p className="font-medium mb-2">How it works:</p>
          <ol className="list-decimal list-inside space-y-1">
            <li>Paste a song URL from any supported service</li>
            <li>We'll find it on other platforms</li>
            <li>Share the universal link with anyone</li>
          </ol>
        </div>
      </div>
    </div>
  );
}
