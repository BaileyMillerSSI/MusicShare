import { ImageResponse } from 'next/og';
import { createElement } from 'react';
import { getPublicMetrics, summarizePublicMetrics } from '../../../lib/server/publicMetrics';
import { MetricsShareImage } from './MetricsShareImage';

export const dynamic = 'force-dynamic';
export const runtime = 'nodejs';

export async function GET() {
  const result = await getPublicMetrics();
  const response = new ImageResponse(
    createElement(MetricsShareImage, { summary: result.available ? summarizePublicMetrics(result.metrics) : undefined }),
    { width: 1200, height: 630 },
  );
  response.headers.set('Content-Type', 'image/png');
  response.headers.set('Cache-Control', 'public, max-age=300, s-maxage=300');
  return response;
}
