import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { DailySongAdditionsChart } from './DailySongAdditionsChart';

const days = Array.from({ length: 7 }, (_, index) => ({
  dayStart: `2026-01-${String(index + 4).padStart(2, '0')}T00:00:00Z`,
  count: [0, 1, 3, 0, 2, 1, 0][index],
}));

describe('DailySongAdditionsChart', () => {
  it('renders one shared UTC graph with seven dated daily buckets and Today', () => {
    render(<DailySongAdditionsChart dailyCompletedSongs={days} largestDailyCount={3} />);
    expect(screen.getByText('Days use UTC calendar boundaries.')).toBeInTheDocument();
    const graph = screen.getByRole('list', { name: /songs added in the last 7 days, utc calendar days/i });
    expect(graph.querySelectorAll('li')).toHaveLength(7);
    expect(screen.getAllByTestId('daily-chart-frame')).toHaveLength(1);
    expect(graph.querySelectorAll('[aria-hidden="true"]')).toHaveLength(7);
    expect(screen.getByLabelText('2026-01-04 UTC: 0 songs added')).toBeInTheDocument();
    expect(screen.getByLabelText('Today, 2026-01-10 UTC: 0 songs added')).toBeInTheDocument();
    expect(screen.getByText('01-10')).toBeInTheDocument();
    expect(screen.getByText('Today')).toBeInTheDocument();
    expect(graph.querySelector('[aria-hidden="true"]')).toHaveStyle({ height: '0%' });
  });

  it('keeps zeroes at zero height and scales positive bars', () => {
    render(<DailySongAdditionsChart dailyCompletedSongs={days} largestDailyCount={3} />);
    const bars = Array.from(screen.getByRole('list').querySelectorAll('[aria-hidden="true"]'));
    expect(bars.map((bar) => bar.getAttribute('style'))).toEqual([
      'height: 0%;', 'height: 33%;', 'height: 100%;', 'height: 0%;', 'height: 67%;', 'height: 33%;', 'height: 0%;',
    ]);
  });

  it('does not divide by zero for an all-zero graph', () => {
    render(<DailySongAdditionsChart dailyCompletedSongs={days.map((day) => ({ ...day, count: 0 }))} largestDailyCount={0} />);
    expect(Array.from(screen.getByRole('list').querySelectorAll('[aria-hidden="true"]'))).toHaveLength(7);
    expect(screen.getAllByText('0')).toHaveLength(7);
  });
});
