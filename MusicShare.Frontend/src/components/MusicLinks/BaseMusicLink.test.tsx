import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import BaseMusicLink from './BaseMusicLink';

describe('BaseMusicLink', () => {
  const defaultProps = {
    url: 'https://example.com/song',
    service: 'Test Service',
    icon: '/test-icon.svg',
    color: 'bg-blue-500',
  };

  it('renders a link with the correct href', () => {
    render(<BaseMusicLink {...defaultProps} />);

    const link = screen.getByRole('link', { name: /test service/i });
    expect(link).toBeVisible();
    expect(link).toHaveAttribute('href', 'https://example.com/song');
  });

  it('opens link in a new tab with security attributes', () => {
    render(<BaseMusicLink {...defaultProps} />);

    const link = screen.getByRole('link', { name: /test service/i });
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('displays the service icon with correct alt text', () => {
    render(<BaseMusicLink {...defaultProps} />);

    const icon = screen.getByAltText('Test Service');
    expect(icon).toBeVisible();
    expect(icon).toHaveAttribute('src', '/test-icon.svg');
  });

  it('displays "Listen on" text', () => {
    render(<BaseMusicLink {...defaultProps} />);

    expect(screen.getByText('Listen on')).toBeVisible();
  });

  it('displays the service name', () => {
    render(<BaseMusicLink {...defaultProps} />);

    expect(screen.getByText('Test Service')).toBeVisible();
  });

  it('applies the provided color class', () => {
    render(<BaseMusicLink {...defaultProps} />);

    const link = screen.getByRole('link', { name: /test service/i });
    expect(link).toHaveClass('bg-blue-500');
  });

  it('applies base styling classes', () => {
    render(<BaseMusicLink {...defaultProps} />);

    const link = screen.getByRole('link', { name: /test service/i });
    expect(link).toHaveClass('flex', 'items-center', 'justify-between');
    expect(link).toHaveClass('text-white', 'py-4', 'px-5', 'rounded-lg');
    expect(link).toHaveClass('transition-all', 'hover:shadow-lg');
    expect(link).toHaveClass('transform', 'hover:-translate-y-0.5');
  });

  it('renders the chevron icon for visual affordance', () => {
    render(<BaseMusicLink {...defaultProps} />);

    const link = screen.getByRole('link', { name: /test service/i });
    const svg = link.querySelector('svg');
    expect(svg).toBeInTheDocument();
    expect(svg).toHaveClass('w-5', 'h-5');
    expect(svg?.querySelector('path')).toHaveAttribute('d', 'M9 5l7 7-7 7');
  });

  it('renders with Spotify-specific props', () => {
    render(
      <BaseMusicLink
        url="https://open.spotify.com/track/123"
        service="Spotify"
        icon="/Spotify_Icon.svg"
        color="bg-[#1ED760]"
      />
    );

    const link = screen.getByRole('link', { name: /spotify/i });
    expect(link).toHaveAttribute('href', 'https://open.spotify.com/track/123');
    expect(screen.getByText('Spotify')).toBeVisible();
    expect(screen.getByAltText('Spotify')).toHaveAttribute('src', '/Spotify_Icon.svg');
    expect(link).toHaveClass('bg-[#1ED760]');
  });

  it('renders with YouTube Music-specific props', () => {
    render(
      <BaseMusicLink
        url="https://music.youtube.com/watch?v=abc"
        service="YouTube Music"
        icon="/YT_Music_Icon.svg"
        color="bg-black"
      />
    );

    const link = screen.getByRole('link', { name: /youtube music/i });
    expect(link).toHaveAttribute('href', 'https://music.youtube.com/watch?v=abc');
    expect(screen.getByText('YouTube Music')).toBeVisible();
    expect(screen.getByAltText('YouTube Music')).toHaveAttribute('src', '/YT_Music_Icon.svg');
    expect(link).toHaveClass('bg-black');
  });

  it('handles empty URL prop', () => {
    const { container } = render(<BaseMusicLink {...defaultProps} url="" />);

    // Empty href doesn't get semantic "link" role, so use querySelector
    const link = container.querySelector('a');
    expect(link).toHaveAttribute('href', '');
  });

  it('handles long service names', () => {
    render(
      <BaseMusicLink
        {...defaultProps}
        service="Super Long Music Streaming Service Name"
      />
    );

    expect(screen.getByText('Super Long Music Streaming Service Name')).toBeVisible();
  });
});
