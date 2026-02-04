'use client';

import { useEffect } from 'react';
import { useQuery } from '@tanstack/react-query';
import Link from 'next/link';
import { api, type ShareResultResponse } from '../lib/api';
import { MusicServiceLink } from './MusicLinks';
import NativeShare from './NativeShare';

interface Props {
  shareId: string;
  initialData?: ShareResultResponse;
}

export function ResultPoller({ shareId, initialData }: Readonly<Props>) {
  const { data, isLoading, isError, error } = useQuery({
    queryKey: ['share', shareId],
    queryFn: () => api.getShareResult(shareId),
    initialData,
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === 'Pending' || status === 'Processing' ? 2000 : false;
    },
  });

  const isProcessing = data?.status === 'Pending' || data?.status === 'Processing';
  const song = data?.song;

  useEffect(() => {
    if (song) document.title = `${song.artists.join(', ')} - ${song.title}`;
  }, [song]);

  // --- Loading ---
  if (isLoading) {
    return (
      <>
        <div className="flex items-center justify-center">
          <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-purple-600" />
        </div>
        <p className="text-center text-gray-600 mt-4">Loading...</p>
      </>
    );
  }

  // --- Error ---
  if (isError) {
    return (
      <>
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded">
          {error.message}
        </div>
        <Link href="/" className="mt-4 inline-block text-purple-600 hover:text-purple-700">
          ← Back to home
        </Link>
      </>
    );
  }

  // --- Result ---
  return (
    <>
      <Link href="/" className="inline-block text-purple-600 hover:text-purple-700 mb-6">
        ← Share another song
      </Link>

      {isProcessing && (
        <div className="bg-blue-50 border border-blue-200 text-blue-700 px-4 py-3 rounded mb-6">
          <div className="flex items-center">
            <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-blue-600 mr-3" />
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
              <img src={song.artworkUrl} alt={song.title} className="w-24 h-24 rounded-lg shadow-md mr-4" />
            )}
            <div className="flex-1">
              <div className="flex items-center gap-2">
                <h1 className="text-2xl font-bold text-gray-800">{song.title}</h1>
                {song.isExplicit && (
                  <span className="inline-flex items-center justify-center w-5 h-5 bg-gray-400 text-white text-xs font-bold rounded">E</span>
                )}
              </div>
              <p className="text-gray-600">{song.artists.join(', ')}</p>
              {song.album && <p className="text-sm text-gray-500 mt-1">{song.album}</p>}
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
            <NativeShare title={song.title} artists={song.artists} />
          </div>
        </div>
      )}
    </>
  );
}
