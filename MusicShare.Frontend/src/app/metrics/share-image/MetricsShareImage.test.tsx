import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { MetricsShareImage } from './MetricsShareImage';

describe('MetricsShareImage', () => {
  it('keeps the title, subtitle, and all four current statistics in two bounded rows', () => {
    render(<MetricsShareImage summary={{
      completedSongs: 123456,
      spotifyLinks: 234567,
      youTubeMusicLinks: 345678,
      thisWeekCompletedSongs: 456789,
    }} />);

    expect(screen.getByText('Music metrics')).toBeInTheDocument();
    expect(screen.getByText('Live public sharing activity')).toBeInTheDocument();
    expect(screen.getByText('Completed songs')).toBeInTheDocument();
    expect(screen.getByText('Spotify links')).toBeInTheDocument();
    expect(screen.getByText('YouTube Music links')).toBeInTheDocument();
    expect(screen.getByText('Completed this week')).toBeInTheDocument();
    expect(screen.getByText('+456,789')).toBeInTheDocument();

    const completedCard = screen.getByText('Completed songs').parentElement;
    const weeklyCard = screen.getByText('Completed this week').parentElement;
    expect(completedCard).toHaveStyle({ width: '48.5%', height: '122px' });
    expect(weeklyCard).toHaveStyle({ width: '48.5%', height: '122px' });
    expect(completedCard?.parentElement).toHaveStyle({ height: '122px' });
    expect(weeklyCard?.parentElement).toHaveStyle({ height: '122px' });
  });
});
