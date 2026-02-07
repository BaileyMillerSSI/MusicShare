import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import SpotifyLink from './SpotifyLink';

describe('SpotifyLink', () => {
  const validUrl = 'https://open.spotify.com/track/123abc';

  it('renders a link with the provided URL', () => {
    render(<SpotifyLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /spotify/i });
    expect(link).toBeVisible();
    expect(link).toHaveAttribute('href', validUrl);
  });

  it('opens link in a new tab with security attributes', () => {
    render(<SpotifyLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /spotify/i });
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('displays the Spotify branding text', () => {
    render(<SpotifyLink url={validUrl} />);

    expect(screen.getByText('Listen on')).toBeVisible();
    expect(screen.getByText('Spotify')).toBeVisible();
  });

  it('displays the Spotify icon with correct alt text', () => {
    render(<SpotifyLink url={validUrl} />);

    const icon = screen.getByAltText('Spotify');
    expect(icon).toBeVisible();
    expect(icon).toHaveAttribute('src', '/Spotify_Icon.svg');
  });

  it('applies Spotify brand color styling', () => {
    render(<SpotifyLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /spotify/i });
    expect(link).toHaveClass('bg-[#1ED760]');
  });

  it('applies base link styling classes', () => {
    render(<SpotifyLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /spotify/i });
    expect(link).toHaveClass('flex', 'items-center', 'justify-between');
    expect(link).toHaveClass('text-white', 'py-4', 'px-5', 'rounded-lg');
    expect(link).toHaveClass('transition-all', 'hover:shadow-lg');
    expect(link).toHaveClass('transform', 'hover:-translate-y-0.5');
  });

  it('renders the chevron icon for visual affordance', () => {
    render(<SpotifyLink url={validUrl} />);

    // The chevron is an SVG without accessible text, so we query by container
    const link = screen.getByRole('link', { name: /spotify/i });
    const svg = link.querySelector('svg');

    expect(svg).toBeInTheDocument();
    expect(svg).toHaveClass('w-5', 'h-5');
  });

  it('handles URLs with special characters', () => {
    const urlWithParams = 'https://open.spotify.com/track/123?si=abc&nd=1';
    render(<SpotifyLink url={urlWithParams} />);

    const link = screen.getByRole('link', { name: /spotify/i });
    expect(link).toHaveAttribute('href', urlWithParams);
  });

  it('handles empty URL prop', () => {
    const { container } = render(<SpotifyLink url="" />);

    // Empty href doesn't create a semantic link role, so we query by tag
    const link = container.querySelector('a');
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '');
    expect(screen.getByText('Spotify')).toBeVisible();
  });

  it('handles URL-like strings that are not valid Spotify URLs', () => {
    const invalidUrl = 'https://example.com/not-spotify';
    render(<SpotifyLink url={invalidUrl} />);

    // Component should still render - validation is not its responsibility
    const link = screen.getByRole('link', { name: /spotify/i });
    expect(link).toHaveAttribute('href', invalidUrl);
  });
});
