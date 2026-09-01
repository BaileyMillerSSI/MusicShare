import type { PublicMetricsSummary } from '../../../lib/server/publicMetrics';

type MetricsShareImageProps = {
  summary?: PublicMetricsSummary;
};

const cardStyle = { display: 'flex', flexDirection: 'column' as const, background: 'white', borderRadius: 28, padding: '48px 56px', width: '1080px', height: '510px' };
const metricStyle = { display: 'flex', flexDirection: 'column' as const, width: '31%', background: '#f5f3ff', borderRadius: 18, padding: '24px' };

export function MetricsShareImage({ summary }: MetricsShareImageProps) {
  const content = summary
    ? [
      ['Completed songs', summary.completedSongs.toLocaleString()],
      ['Spotify links', summary.spotifyLinks.toLocaleString()],
      ['YouTube Music links', summary.youTubeMusicLinks.toLocaleString()],
    ]
    : [];

  return <div style={{ display: 'flex', width: '100%', height: '100%', background: 'linear-gradient(135deg, #a855f7, #ec4899)', padding: '60px' }}>
    <div style={cardStyle}>
      <div style={{ display: 'flex', color: '#7e22ce', fontSize: 28, fontWeight: 700 }}>MusicShare</div>
      <div style={{ display: 'flex', marginTop: 12, color: '#1f2937', fontSize: 52, fontWeight: 800 }}>Music metrics</div>
      {summary ? <>
        <div style={{ display: 'flex', marginTop: 16, color: '#6b7280', fontSize: 24 }}>Live public sharing activity</div>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 34 }}>
          {content.map(([label, count]) => <div key={label} style={metricStyle}><div style={{ display: 'flex', color: '#4b5563', fontSize: 20 }}>{label}</div><div style={{ display: 'flex', color: '#1f2937', fontSize: 40, fontWeight: 800, marginTop: 12 }}>{count}</div></div>)}
        </div>
        <div style={{ display: 'flex', marginTop: 26, color: '#7e22ce', fontSize: 25, fontWeight: 700 }}>+{summary.thisWeekCompletedSongs.toLocaleString()} completed this week</div>
      </> : <div style={{ display: 'flex', marginTop: 28, color: '#4b5563', fontSize: 30 }}>Explore public sharing activity and resolved platform links.</div>}
    </div>
  </div>;
}
