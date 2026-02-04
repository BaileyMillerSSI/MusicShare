import { type ShareResultResponse } from '../../../lib/api';
import { ResultPoller } from '../../../components/ResultPoller';

export const revalidate = false; // cache indefinitely; revalidated on-demand by the Worker

interface PageProps {
  params: Promise<{ shareId: string }>;
}

export default async function ShareResultPage({ params }: PageProps) {
  const { shareId } = await params;

  // Absolute URL — relative paths don't resolve in server-side fetch
  const apiBase =
    process.env.services__api__https__0 ??
    process.env.services__api__http__0 ??
    'http://localhost:5222';

  const res = await fetch(`${apiBase}/api/share/${shareId}`);
  const data: ShareResultResponse = await res.json();

  return (
    <div className="min-h-screen bg-linear-to-br from-purple-500 to-pink-500 flex items-center justify-center p-4">
      <div className="bg-white rounded-lg shadow-xl p-8 max-w-2xl w-full">
        {/* Pass initialData only when Completed; otherwise client polls fresh */}
        <ResultPoller
          shareId={shareId}
          initialData={data.status === 'Completed' ? data : undefined}
        />
      </div>
    </div>
  );
}
