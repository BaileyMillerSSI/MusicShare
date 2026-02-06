import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import AppleMusicLink from './AppleMusicLink';

describe('AppleMusicLink', () => {
  const validUrl = 'https://music.apple.com/us/album/test-album/123';

  it('renders a link with the provided URL', () => {
    render(<AppleMusicLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /apple music/i });
    expect(link).toBeVisible();
    expect(link).toHaveAttribute('href', validUrl);
  });

  it('opens link in a new tab with security attributes', () => {
    render(<AppleMusicLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /apple music/i });
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('displays the Apple Music branding text', () => {
    render(<AppleMusicLink url={validUrl} />);

    expect(screen.getByText('Listen on')).toBeVisible();
    expect(screen.getByText('Apple Music')).toBeVisible();
  });

  it('displays the Apple Music icon with correct alt text', () => {
    render(<AppleMusicLink url={validUrl} />);

    const icon = screen.getByAltText('Apple Music');
    expect(icon).toBeVisible();
    expect(icon).toHaveAttribute('src', '/Apple_Music_Icon.svg');
  });

  it('applies Apple Music brand color styling', () => {
    render(<AppleMusicLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /apple music/i });
    expect(link).toHaveClass('bg-black');
  });

  it('applies base link styling classes', () => {
    render(<AppleMusicLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /apple music/i });
    expect(link).toHaveClass('flex', 'items-center', 'justify-between');
    expect(link).toHaveClass('text-white', 'py-4', 'px-5', 'rounded-lg');
    expect(link).toHaveClass('transition-all', 'hover:shadow-lg');
    expect(link).toHaveClass('transform', 'hover:-translate-y-0.5');
  });

  it('renders the chevron icon for visual affordance', () => {
    render(<AppleMusicLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /apple music/i });
    const svg = link.querySelector('svg');

    expect(svg).toBeInTheDocument();
    expect(svg).toHaveClass('w-5', 'h-5');
    expect(svg?.querySelector('path')).toHaveAttribute('d', 'M9 5l7 7-7 7');
  });

  it('handles various Apple Music URL formats', () => {
    const { rerender } = render(<AppleMusicLink url="https://music.apple.com/us/album/test-album/123" />);
    let link = screen.getByRole('link', { name: /apple music/i });
    expect(link).toHaveAttribute('href', 'https://music.apple.com/us/album/test-album/123');

    rerender(<AppleMusicLink url="https://music.apple.com/gb/song/test-song/456" />);
    link = screen.getByRole('link', { name: /apple music/i });
    expect(link).toHaveAttribute('href', 'https://music.apple.com/gb/song/test-song/456');

    rerender(<AppleMusicLink url="https://music.apple.com/ca/playlist/test-playlist/789" />);
    link = screen.getByRole('link', { name: /apple music/i });
    expect(link).toHaveAttribute('href', 'https://music.apple.com/ca/playlist/test-playlist/789');
  });

  it('handles URLs with special characters', () => {
    const urlWithParams = 'https://music.apple.com/us/album/123?i=456&app=music';
    render(<AppleMusicLink url={urlWithParams} />);

    const link = screen.getByRole('link', { name: /apple music/i });
    expect(link).toHaveAttribute('href', urlWithParams);
  });

  it('handles empty URL prop', () => {
    const { container } = render(<AppleMusicLink url="" />);

    // Empty href doesn't create a semantic link role, so we query by tag
    const link = container.querySelector('a');
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '');
    expect(screen.getByText('Apple Music')).toBeVisible();
  });

  it('handles URL-like strings that are not valid Apple Music URLs', () => {
    const invalidUrl = 'https://example.com/not-apple-music';
    render(<AppleMusicLink url={invalidUrl} />);

    // Component should still render - validation is not its responsibility
    const link = screen.getByRole('link', { name: /apple music/i });
    expect(link).toHaveAttribute('href', invalidUrl);
  });
});
