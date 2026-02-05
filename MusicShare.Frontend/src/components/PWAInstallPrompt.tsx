'use client';

import { usePWAInstall } from '../hooks/usePWAInstall';

function IOSInstallInstructions({
  onDismiss,
}: Readonly<{ onDismiss: () => void }>) {
  return (
    <div className="fixed inset-0 bg-black/50 flex items-end sm:items-center justify-center z-50 p-4">
      <div className="bg-white rounded-t-xl sm:rounded-xl max-w-md w-full p-6">
        <h2 className="text-xl font-bold text-gray-800 mb-4">
          Install MusicShare
        </h2>
        <p className="text-gray-600 mb-6">
          Add MusicShare to your home screen for the best experience:
        </p>

        <ol className="space-y-4 mb-6">
          <li className="flex items-start gap-3">
            <span className="flex-shrink-0 w-6 h-6 bg-purple-100 text-purple-600 rounded-full flex items-center justify-center text-sm font-medium">
              1
            </span>
            <span className="text-gray-700">
              Tap the{' '}
              <svg
                className="inline w-5 h-5 text-blue-500"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  strokeWidth={2}
                  d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12"
                />
              </svg>{' '}
              <strong>Share</strong> button in Safari
            </span>
          </li>
          <li className="flex items-start gap-3">
            <span className="flex-shrink-0 w-6 h-6 bg-purple-100 text-purple-600 rounded-full flex items-center justify-center text-sm font-medium">
              2
            </span>
            <span className="text-gray-700">
              Scroll down and tap <strong>Add to Home Screen</strong>
            </span>
          </li>
          <li className="flex items-start gap-3">
            <span className="flex-shrink-0 w-6 h-6 bg-purple-100 text-purple-600 rounded-full flex items-center justify-center text-sm font-medium">
              3
            </span>
            <span className="text-gray-700">
              Tap <strong>Add</strong> to confirm
            </span>
          </li>
        </ol>

        <button
          onClick={onDismiss}
          className="w-full bg-purple-600 text-white py-3 rounded-lg hover:bg-purple-700 transition-colors font-medium cursor-pointer"
        >
          Got it
        </button>
      </div>
    </div>
  );
}

function InstallBanner({
  onInstall,
  onDismiss,
}: Readonly<{ onInstall: () => void; onDismiss: () => void }>) {
  return (
    <div className="fixed bottom-0 md:bottom-auto md:top-0 left-0 right-0 bg-white border-t md:border-t-0 md:border-b border-gray-200 shadow-lg p-4 z-50">
      <div className="max-w-2xl mx-auto flex items-center justify-between gap-4">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-purple-100 rounded-lg flex items-center justify-center">
            <svg
              className="w-6 h-6 text-purple-600"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4"
              />
            </svg>
          </div>
          <div>
            <p className="font-medium text-gray-800">Install MusicShare</p>
            <p className="text-sm text-gray-600">
              Add to home screen for quick access
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <button
            onClick={onDismiss}
            className="text-gray-500 hover:text-gray-700 px-3 py-2 cursor-pointer"
          >
            Not now
          </button>
          <button
            onClick={onInstall}
            className="bg-purple-600 text-white px-4 py-2 rounded-lg hover:bg-purple-700 transition-colors cursor-pointer"
          >
            Install
          </button>
        </div>
      </div>
    </div>
  );
}

export function PWAInstallPrompt() {
  const { canInstall, isIOSDevice, promptInstall, dismissPrompt } =
    usePWAInstall();

  if (!canInstall) return null;

  if (isIOSDevice) {
    return <IOSInstallInstructions onDismiss={dismissPrompt} />;
  }

  return <InstallBanner onInstall={promptInstall} onDismiss={dismissPrompt} />;
}
