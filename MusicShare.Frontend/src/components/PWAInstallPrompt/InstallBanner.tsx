interface InstallBannerProps {
  onInstall: () => void;
  onDismiss: () => void;
}

export default function InstallBanner({
  onInstall,
  onDismiss,
}: Readonly<InstallBannerProps>) {
  return (
    <div className="fixed bottom-0 left-0 right-0 bg-white border-t md:border-t-0 md:border-b lg:border-b-0 lg:border-t border-gray-200 shadow-lg p-4 z-50">
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
            <p className="font-medium text-gray-800">Install Music Share</p>
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
