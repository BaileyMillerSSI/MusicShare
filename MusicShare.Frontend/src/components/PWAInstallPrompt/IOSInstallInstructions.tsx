interface IOSInstallInstructionsProps {
  onDismiss: () => void;
}

export default function IOSInstallInstructions({
  onDismiss,
}: Readonly<IOSInstallInstructionsProps>) {
  return (
    <div className="fixed inset-0 bg-black/50 flex items-end justify-center z-50 md:p-4">
      <div className="bg-white rounded-xl max-w-md w-full p-6">
        <h2 className="text-xl font-bold text-gray-800 mb-4">
          Install Music Share
        </h2>
        <p className="text-gray-600 mb-6">
          Add Music Share to your home screen for the best experience:
        </p>

        <ol className="space-y-4 mb-6">
          <li className="flex items-start gap-3">
            <span className="shrink-0 w-6 h-6 bg-purple-100 text-purple-600 rounded-full flex items-center justify-center text-sm font-medium">
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
            <span className="shrink-0 w-6 h-6 bg-purple-100 text-purple-600 rounded-full flex items-center justify-center text-sm font-medium">
              2
            </span>
            <span className="text-gray-700">
              Scroll down and tap <strong>Add to Home Screen</strong>
            </span>
          </li>
          <li className="flex items-start gap-3">
            <span className="shrink-0 w-6 h-6 bg-purple-100 text-purple-600 rounded-full flex items-center justify-center text-sm font-medium">
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
