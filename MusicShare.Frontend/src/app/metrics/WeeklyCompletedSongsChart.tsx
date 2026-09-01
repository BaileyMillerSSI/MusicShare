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
  return {
    accessible: `${weekStart.slice(0, 10)} UTC: ${count} songs`,
    visible: `${weekStart.slice(5, 10)} UTC`,
  };
}

function localWeekLabels(weekStart: string, count: number): WeekLabels {
  const date = new Date(weekStart);
  const visible = new Intl.DateTimeFormat(undefined, {
    month: '2-digit',
    day: '2-digit',
    hour: 'numeric',
    minute: '2-digit',
    timeZoneName: 'short',
  }).format(date);
  const accessible = new Intl.DateTimeFormat(undefined, {
    weekday: 'long',
    year: 'numeric',
    month: 'long',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    timeZoneName: 'long',
  }).format(date);

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

  return <ol className="mt-3 grid grid-cols-8 gap-1.5" aria-label="Songs by week, Sunday UTC start">
    {weeklyCompletedSongs.map((week) => {
      const height = week.count === 0 || largestWeeklyCount === 0 ? 0 : Math.max(8, Math.round((week.count / largestWeeklyCount) * 100));
      const labels = weekLabels(week.weekStart, week.count, useLocalTime);
      return <li key={week.weekStart} className="min-w-0 text-center text-xs text-gray-600" aria-label={labels.accessible}>
        <span className="flex h-28 items-end justify-center rounded bg-gray-100 px-0.5"><span className="w-full rounded-t bg-purple-600" style={{ height: `${height}%` }} aria-hidden="true" /></span>
        <span className="mt-1 block font-medium text-gray-800">{week.count}</span>
        <time className="block text-[10px] leading-tight" dateTime={week.weekStart}>{labels.visible}</time>
      </li>;
    })}
  </ol>;
}
