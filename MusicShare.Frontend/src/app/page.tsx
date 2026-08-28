import { Suspense } from 'react';
import { BreadstickFooter } from '../components/BreadstickFooter';
import { ShareForm } from '../components/ShareForm';

export default function Home() {
  return (
    <div className="min-h-screen bg-linear-to-br from-purple-500 to-pink-500 flex flex-col items-center justify-center gap-4 p-4">
      <div className="bg-white rounded-lg shadow-xl p-8 max-w-md w-full">
        <h1 className="text-3xl font-bold text-gray-800 mb-2">Music Share</h1>
        <p className="text-gray-600 mb-6">Share music across platforms</p>
        <Suspense fallback={<div className="animate-pulse h-48 bg-gray-100 rounded-lg" />}>
          <ShareForm />
        </Suspense>
        <div className="mt-6 text-sm text-gray-600">
          <p className="font-medium mb-2">How it works:</p>
          <ol className="list-decimal list-inside space-y-1">
            <li>Paste a song URL from any supported service</li>
            <li>We&apos;ll find it on other platforms</li>
            <li>Share the universal link with anyone</li>
          </ol>
        </div>
      </div>
      <BreadstickFooter />
    </div>
  );
}
