import type { PublicMetricsDailyCompletedSong } from '../../lib/api';

type DailySongAdditionsChartProps = {
  dailyCompletedSongs: PublicMetricsDailyCompletedSong[];
  largestDailyCount: number;
};

function labels(dayStart: string, count: number, isToday: boolean) {
  const day = dayStart.slice(0, 10);
  return {
    accessible: `${isToday ? 'Today, ' : ''}${day} UTC: ${count} songs added`,
    visibleDate: day.slice(5),
  };
}

export function DailySongAdditionsChart({ dailyCompletedSongs, largestDailyCount }: DailySongAdditionsChartProps) {
  return <>
    <p className="mt-2 text-xs text-gray-600">Days use UTC calendar boundaries.</p>
    <div className="mt-3 h-28 rounded bg-gray-100 px-1.5 pt-1.5" data-testid="daily-chart-frame">
      <ol className="grid h-full grid-cols-7 gap-1.5" aria-label="Songs added in the last 7 days, UTC calendar days">
        {dailyCompletedSongs.map((day, index) => {
          const height = day.count === 0 || largestDailyCount === 0 ? 0 : Math.max(8, Math.round((day.count / largestDailyCount) * 100));
          const label = labels(day.dayStart, day.count, index === dailyCompletedSongs.length - 1);
          return <li key={day.dayStart} className="flex min-w-0 items-end justify-center" aria-label={label.accessible}>
            <span className="w-full rounded-t bg-purple-600" style={{ height: `${height}%` }} aria-hidden="true" />
          </li>;
        })}
      </ol>
    </div>
    <div className="grid grid-cols-7 gap-1.5">
      {dailyCompletedSongs.map((day, index) => {
        const label = labels(day.dayStart, day.count, index === dailyCompletedSongs.length - 1);
        return <div key={day.dayStart} className="min-w-0 text-center text-xs text-gray-600">
          <span className="mt-1 block font-medium text-gray-800">{day.count}</span>
          <time className="block text-[10px] leading-tight" dateTime={day.dayStart}>{label.visibleDate}</time>
          {index === dailyCompletedSongs.length - 1 ? <span className="block text-[10px] font-medium leading-tight text-purple-700">Today</span> : null}
        </div>;
      })}
    </div>
  </>;
}
