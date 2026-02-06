import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import YouTubeMusicLink from './YouTubeMusicLink';

describe('YouTubeMusicLink', () => {
  const validUrl = 'https://music.youtube.com/watch?v=dQw4w9WgXcQ';

  it('renders a link with the provided URL', () => {
    render(<YouTubeMusicLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /youtube music/i });
    expect(link).toBeVisible();
    expect(link).toHaveAttribute('href', validUrl);
  });

  it('opens link in a new tab with security attributes', () => {
    render(<YouTubeMusicLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /youtube music/i });
    expect(link).toHaveAttribute('target', '_blank');
    expect(link).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('displays the YouTube Music branding text', () => {
    render(<YouTubeMusicLink url={validUrl} />);

    expect(screen.getByText('Listen on')).toBeVisible();
    expect(screen.getByText('YouTube Music')).toBeVisible();
  });

  it('displays the YouTube Music icon with correct alt text', () => {
    render(<YouTubeMusicLink url={validUrl} />);

    const icon = screen.getByAltText('YouTube Music');
    expect(icon).toBeVisible();
    expect(icon).toHaveAttribute('src', '/YT_Music_Icon.svg');
  });

  it('applies YouTube Music brand color styling', () => {
    render(<YouTubeMusicLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /youtube music/i });
    expect(link).toHaveClass('bg-black');
  });

  it('applies base link styling classes', () => {
    render(<YouTubeMusicLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /youtube music/i });
    expect(link).toHaveClass('flex', 'items-center', 'justify-between');
    expect(link).toHaveClass('text-white', 'py-4', 'px-5', 'rounded-lg');
    expect(link).toHaveClass('transition-all', 'hover:shadow-lg');
    expect(link).toHaveClass('transform', 'hover:-translate-y-0.5');
  });

  it('renders the chevron icon for visual affordance', () => {
    render(<YouTubeMusicLink url={validUrl} />);

    const link = screen.getByRole('link', { name: /youtube music/i });
    const svg = link.querySelector('svg');

    expect(svg).toBeInTheDocument();
    expect(svg).toHaveClass('w-5', 'h-5');
  });

  it('handles various YouTube Music URL formats', () => {
    const { rerender } = render(<YouTubeMusicLink url="https://music.youtube.com/watch?v=dQw4w9WgXcQ" />);
    let link = screen.getByRole('link', { name: /youtube music/i });
    expect(link).toHaveAttribute('href', 'https://music.youtube.com/watch?v=dQw4w9WgXcQ');

    rerender(<YouTubeMusicLink url="https://music.youtube.com/playlist?list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf" />);
    link = screen.getByRole('link', { name: /youtube music/i });
    expect(link).toHaveAttribute('href', 'https://music.youtube.com/playlist?list=PLrAXtmErZgOeiKm4sgNOknGvNjby9efdf');

    rerender(<YouTubeMusicLink url="https://music.youtube.com/channel/UCTest123" />);
    link = screen.getByRole('link', { name: /youtube music/i });
    expect(link).toHaveAttribute('href', 'https://music.youtube.com/channel/UCTest123');
  });

  it('handles URLs with special characters', () => {
    const urlWithParams = 'https://music.youtube.com/watch?v=abc123&list=PL456&index=1';
    render(<YouTubeMusicLink url={urlWithParams} />);

    const link = screen.getByRole('link', { name: /youtube music/i });
    expect(link).toHaveAttribute('href', urlWithParams);
  });

  it('handles empty URL prop', () => {
    const { container } = render(<YouTubeMusicLink url="" />);

    // Empty href doesn't create a semantic link role, so we query by tag
    const link = container.querySelector('a');
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '');
    expect(screen.getByText('YouTube Music')).toBeVisible();
  });

  it('handles URL-like strings that are not valid YouTube Music URLs', () => {
    const invalidUrl = 'https://example.com/not-youtube-music';
    render(<YouTubeMusicLink url={invalidUrl} />);

    // Component should still render - validation is not its responsibility
    const link = screen.getByRole('link', { name: /youtube music/i });
    expect(link).toHaveAttribute('href', invalidUrl);
  });
});
