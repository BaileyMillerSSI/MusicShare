import { act, render, screen } from '@testing-library/react';
import { hydrateRoot } from 'react-dom/client';
import { renderToString } from 'react-dom/server';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { WeeklyCompletedSongsChart } from './WeeklyCompletedSongsChart';

const weeklyCompletedSongs = [
  { weekStart: '2026-01-04T00:00:00Z', count: 0 },
  { weekStart: '2026-01-11T00:00:00Z', count: 3 },
];

afterEach(() => vi.restoreAllMocks());

describe('WeeklyCompletedSongsChart', () => {
  it('renders deterministic UTC labels in server markup', () => {
    const html = renderToString(<WeeklyCompletedSongsChart weeklyCompletedSongs={weeklyCompletedSongs} largestWeeklyCount={3} />);

    expect(html).toContain('2026-01-04 00:00 UTC through 2026-01-11 00:00 UTC (Sunday UTC week): 0 songs');
    expect(html).toContain('01-04–01-10 UTC');
    expect(html).toContain('This week');
    expect(html).toContain('Weeks use Sunday UTC boundaries. Historical date ranges adapt to your local calendar after the page loads.');
    expect(html).toContain('dateTime="2026-01-11T00:00:00Z"');
  });

  it('uses browser-local labels after mount, including a prior local calendar day', async () => {
    const dateTimeFormat = vi.spyOn(Intl, 'DateTimeFormat').mockImplementation(function MockDateTimeFormat(_locale, options) {
      return { format: () => options?.weekday === 'long' ? 'Saturday, January 3, 2026' : '01/03' } as Intl.DateTimeFormat;
    });

    render(<WeeklyCompletedSongsChart weeklyCompletedSongs={weeklyCompletedSongs} largestWeeklyCount={3} />);

    expect(await screen.findByLabelText('Saturday, January 3, 2026 through Saturday, January 3, 2026 (Sunday UTC week, displayed in local time): 0 songs')).toBeInTheDocument();
    expect(screen.getByText('01/03–01/03')).toBeInTheDocument();
    expect(screen.getByText('This week')).toBeInTheDocument();
    expect(screen.getByLabelText(/Saturday/).querySelector('[aria-hidden="true"]')).toHaveStyle({ height: '0%' });
    expect(dateTimeFormat).toHaveBeenCalledTimes(2);
  });

  it('preserves bucket identity, counts, and bar heights while localizing labels', async () => {
    vi.spyOn(Intl, 'DateTimeFormat').mockImplementation(function MockDateTimeFormat(_locale, options) {
      return { format: () => options?.weekday === 'long' ? 'Local accessible date' : 'Local visible date' } as Intl.DateTimeFormat;
    });

    render(<WeeklyCompletedSongsChart weeklyCompletedSongs={weeklyCompletedSongs} largestWeeklyCount={3} />);

    const first = await screen.findByLabelText('Local accessible date through Local accessible date (Sunday UTC week, displayed in local time): 0 songs');
    const second = screen.getByLabelText('This week (Sunday UTC week): 3 songs');
    expect(first.querySelector('time')).toHaveAttribute('dateTime', '2026-01-04T00:00:00Z');
    expect(second.querySelector('time')).toHaveAttribute('dateTime', '2026-01-11T00:00:00Z');
    expect(first.querySelector('[aria-hidden="true"]')).toHaveStyle({ height: '0%' });
    expect(second.querySelector('[aria-hidden="true"]')).toHaveStyle({ height: '100%' });
  });

  it('retains UTC labels when local formatting fails', async () => {
    vi.spyOn(Intl, 'DateTimeFormat').mockImplementation(function MockDateTimeFormat() {
      throw new Error('formatting unavailable');
    });

    render(<WeeklyCompletedSongsChart weeklyCompletedSongs={weeklyCompletedSongs} largestWeeklyCount={3} />);

    expect(await screen.findByLabelText('2026-01-04 00:00 UTC through 2026-01-11 00:00 UTC (Sunday UTC week): 0 songs')).toBeInTheDocument();
    expect(screen.getByText('01-04–01-10 UTC')).toBeInTheDocument();
  });

  it('hydrates the UTC server fallback without mismatch diagnostics', async () => {
    const serverHtml = renderToString(<WeeklyCompletedSongsChart weeklyCompletedSongs={weeklyCompletedSongs} largestWeeklyCount={3} />);
    const container = document.createElement('div');
    container.innerHTML = serverHtml;
    const initialHtml = container.innerHTML;
    const recoverableErrors: unknown[] = [];
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});

    const root = hydrateRoot(container, <WeeklyCompletedSongsChart weeklyCompletedSongs={weeklyCompletedSongs} largestWeeklyCount={3} />, {
      onRecoverableError: (error) => recoverableErrors.push(error),
    });

    expect(container.innerHTML).toBe(initialHtml);
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 0));
    });
    expect(recoverableErrors).toEqual([]);
    expect(consoleError).not.toHaveBeenCalled();
    root.unmount();
  });
});
