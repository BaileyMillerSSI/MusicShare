import type { PublicMetricsSummary } from '../../../lib/server/publicMetrics';

type MetricsShareImageProps = {
  summary?: PublicMetricsSummary;
};

const cardStyle = {
  display: 'flex',
  flexDirection: 'column' as const,
  background: 'white',
  borderRadius: 28,
  boxSizing: 'border-box' as const,
  padding: '40px 48px',
  width: '100%',
  height: '100%',
};
const metricRowStyle = { display: 'flex', justifyContent: 'space-between', width: '100%', height: '122px' };
const headerStyle = { display: 'flex', flexDirection: 'column' as const, height: '122px', flexShrink: 0 };
const metricStyle = {
  display: 'flex',
  flexDirection: 'column' as const,
  width: '48.5%',
  height: '122px',
  background: '#f5f3ff',
  borderRadius: 18,
  padding: '18px 22px',
};

export function MetricsShareImage({ summary }: MetricsShareImageProps) {
  const content = summary
    ? [
      ['Completed songs', summary.completedSongs.toLocaleString()],
      ['Spotify links', summary.spotifyLinks.toLocaleString()],
      ['YouTube Music links', summary.youTubeMusicLinks.toLocaleString()],
      ['Completed this week', `+${summary.thisWeekCompletedSongs.toLocaleString()}`],
    ]
    : [];

  return <div style={{ display: 'flex', boxSizing: 'border-box', width: '100%', height: '100%', background: 'linear-gradient(135deg, #a855f7, #ec4899)', padding: '40px' }}>
    <div style={cardStyle}>
      <div style={headerStyle}>
        <div style={{ display: 'flex', color: '#7e22ce', fontSize: 24, fontWeight: 700 }}>MusicShare</div>
        <div style={{ display: 'flex', marginTop: 6, color: '#1f2937', fontSize: 42, fontWeight: 800 }}>Music metrics</div>
        {summary && <div style={{ display: 'flex', marginTop: 8, color: '#6b7280', fontSize: 21 }}>Live public sharing activity</div>}
      </div>
      {summary ? <>
        <div style={{ display: 'flex', flexDirection: 'column', justifyContent: 'space-between', flexShrink: 0, marginTop: 20, height: '260px', width: '100%' }}>
          {[content.slice(0, 2), content.slice(2)].map((row, index) => <div key={index} style={metricRowStyle}>
            {row.map(([label, count]) => <div key={label} style={metricStyle}>
              <div style={{ display: 'flex', color: '#4b5563', fontSize: 18 }}>{label}</div>
              <div style={{ display: 'flex', color: '#1f2937', fontSize: 36, fontWeight: 800, marginTop: 8 }}>{count}</div>
            </div>)}
          </div>)}
        </div>
      </> : <div style={{ display: 'flex', marginTop: 16, color: '#4b5563', fontSize: 30 }}>Explore public sharing activity and resolved platform links.</div>}
    </div>
  </div>;
}
