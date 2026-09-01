'use client';

import { useEffect, useState } from 'react';
import type { PublicMetricsWeeklyCompletedSong } from '../../lib/api';

type WeeklyCompletedSongsChartProps = {
  weeklyCompletedSongs: PublicMetricsWeeklyCompletedSong[];
  largestWeeklyCount: number;
};

type WeekLabels = {
  accessible: string;
  visible: string;
};

function utcWeekLabels(weekStart: string, count: number): WeekLabels {
  const startDate = new Date(weekStart);
  const weekEnd = new Date(startDate.getTime() + (6 * 24 * 60 * 60 * 1000));
  const nextWeekStart = new Date(startDate.getTime() + (7 * 24 * 60 * 60 * 1000));
  const start = weekStart.slice(0, 10);
  const end = weekEnd.toISOString().slice(0, 10);
  return {
    accessible: `${start} 00:00 UTC through ${nextWeekStart.toISOString().slice(0, 10)} 00:00 UTC (Sunday UTC week): ${count} songs`,
    visible: `${start.slice(5)}–${end.slice(5)} UTC`,
  };
}

function localWeekLabels(weekStart: string, count: number): WeekLabels {
  const start = new Date(weekStart);
  const visibleEnd = new Date(start.getTime() + (6 * 24 * 60 * 60 * 1000));
  const nextWeekStart = new Date(start.getTime() + (7 * 24 * 60 * 60 * 1000));
  const visibleFormatter = new Intl.DateTimeFormat(undefined, {
    month: '2-digit',
    day: '2-digit',
  });
  const accessibleFormatter = new Intl.DateTimeFormat(undefined, {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    timeZoneName: 'long',
  });
  const visible = `${visibleFormatter.format(start)}–${visibleFormatter.format(visibleEnd)}`;
  const accessible = `${accessibleFormatter.format(start)} through ${accessibleFormatter.format(nextWeekStart)} (Sunday UTC week, displayed in local time)`;

  return { accessible: `${accessible}: ${count} songs`, visible };
}

function weekLabels(weekStart: string, count: number, useLocalTime: boolean): WeekLabels {
  const utcLabels = utcWeekLabels(weekStart, count);
  if (!useLocalTime) return utcLabels;

  try {
    return localWeekLabels(weekStart, count);
  } catch {
    return utcLabels;
  }
}

export function WeeklyCompletedSongsChart({ weeklyCompletedSongs, largestWeeklyCount }: WeeklyCompletedSongsChartProps) {
  const [useLocalTime, setUseLocalTime] = useState(false);

  useEffect(() => {
    const timer = setTimeout(() => setUseLocalTime(true), 0);
    return () => clearTimeout(timer);
  }, []);

  return <>
    <p className="mt-2 text-xs text-gray-600">Weeks use Sunday UTC boundaries. Historical date ranges adapt to your local calendar after the page loads.</p>
    <ol className="mt-3 grid grid-cols-8 gap-1.5" aria-label="Songs by week, Sunday UTC boundaries; historical date ranges display in local time after page load">
    {weeklyCompletedSongs.map((week, index) => {
      const height = week.count === 0 || largestWeeklyCount === 0 ? 0 : Math.max(8, Math.round((week.count / largestWeeklyCount) * 100));
      const isCurrentWeek = index === weeklyCompletedSongs.length - 1;
      const labels = isCurrentWeek
        ? { visible: 'This week', accessible: `This week (Sunday UTC week): ${week.count} songs` }
        : weekLabels(week.weekStart, week.count, useLocalTime);
      return <li key={week.weekStart} className="min-w-0 text-center text-xs text-gray-600" aria-label={labels.accessible}>
        <span className="flex h-28 items-end justify-center rounded bg-gray-100 px-0.5"><span className="w-full rounded-t bg-purple-600" style={{ height: `${height}%` }} aria-hidden="true" /></span>
        <span className="mt-1 block font-medium text-gray-800">{week.count}</span>
        <time className="block text-[10px] leading-tight" dateTime={week.weekStart}>{labels.visible}</time>
      </li>;
    })}
    </ol>
  </>;
}
