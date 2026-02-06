import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { MusicServiceType } from '../../lib/api';
import MusicServiceLink from './MusicServiceLink';

describe('MusicServiceLink', () => {
  it('renders SpotifyLink when serviceType is Spotify', () => {
    const link = {
      serviceType: MusicServiceType.Spotify,
      url: 'https://open.spotify.com/track/123',
    };

    render(<MusicServiceLink link={link} />);

    expect(screen.getByText('Spotify')).toBeVisible();
    expect(screen.getByAltText('Spotify')).toBeVisible();
    const linkElement = screen.getByRole('link', { name: /spotify/i });
    expect(linkElement).toHaveAttribute('href', 'https://open.spotify.com/track/123');
  });

  it('renders AppleMusicLink when serviceType is AppleMusic', () => {
    const link = {
      serviceType: MusicServiceType.AppleMusic,
      url: 'https://music.apple.com/us/album/123',
    };

    render(<MusicServiceLink link={link} />);

    expect(screen.getByText('Apple Music')).toBeVisible();
    expect(screen.getByAltText('Apple Music')).toBeVisible();
    const linkElement = screen.getByRole('link', { name: /apple music/i });
    expect(linkElement).toHaveAttribute('href', 'https://music.apple.com/us/album/123');
  });

  it('renders YouTubeMusicLink when serviceType is YouTubeMusic', () => {
    const link = {
      serviceType: MusicServiceType.YouTubeMusic,
      url: 'https://music.youtube.com/watch?v=abc123',
    };

    render(<MusicServiceLink link={link} />);

    expect(screen.getByText('YouTube Music')).toBeVisible();
    expect(screen.getByAltText('YouTube Music')).toBeVisible();
    const linkElement = screen.getByRole('link', { name: /youtube music/i });
    expect(linkElement).toHaveAttribute('href', 'https://music.youtube.com/watch?v=abc123');
  });

  it('renders null for unknown service type', () => {
    const link = {
      serviceType: 999 as MusicServiceType,
      url: 'https://unknown.com/track/123',
    };

    const { container } = render(<MusicServiceLink link={link} />);

    expect(container.innerHTML).toBe('');
  });

  it('passes the correct URL to each service component', () => {
    const spotifyLink = {
      serviceType: MusicServiceType.Spotify,
      url: 'https://open.spotify.com/track/spotify123',
    };

    const { rerender } = render(<MusicServiceLink link={spotifyLink} />);
    let linkElement = screen.getByRole('link', { name: /spotify/i });
    expect(linkElement).toHaveAttribute('href', 'https://open.spotify.com/track/spotify123');

    const appleMusicLink = {
      serviceType: MusicServiceType.AppleMusic,
      url: 'https://music.apple.com/us/album/apple456',
    };

    rerender(<MusicServiceLink link={appleMusicLink} />);
    linkElement = screen.getByRole('link', { name: /apple music/i });
    expect(linkElement).toHaveAttribute('href', 'https://music.apple.com/us/album/apple456');

    const youtubeMusicLink = {
      serviceType: MusicServiceType.YouTubeMusic,
      url: 'https://music.youtube.com/watch?v=youtube789',
    };

    rerender(<MusicServiceLink link={youtubeMusicLink} />);
    linkElement = screen.getByRole('link', { name: /youtube music/i });
    expect(linkElement).toHaveAttribute('href', 'https://music.youtube.com/watch?v=youtube789');
  });

  it('renders each link with proper accessibility attributes', () => {
    const spotifyLink = {
      serviceType: MusicServiceType.Spotify,
      url: 'https://open.spotify.com/track/123',
    };

    const { rerender } = render(<MusicServiceLink link={spotifyLink} />);
    let linkElement = screen.getByRole('link', { name: /spotify/i });
    expect(linkElement).toHaveAttribute('target', '_blank');
    expect(linkElement).toHaveAttribute('rel', 'noopener noreferrer');

    const appleMusicLink = {
      serviceType: MusicServiceType.AppleMusic,
      url: 'https://music.apple.com/us/album/123',
    };

    rerender(<MusicServiceLink link={appleMusicLink} />);
    linkElement = screen.getByRole('link', { name: /apple music/i });
    expect(linkElement).toHaveAttribute('target', '_blank');
    expect(linkElement).toHaveAttribute('rel', 'noopener noreferrer');

    const youtubeMusicLink = {
      serviceType: MusicServiceType.YouTubeMusic,
      url: 'https://music.youtube.com/watch?v=123',
    };

    rerender(<MusicServiceLink link={youtubeMusicLink} />);
    linkElement = screen.getByRole('link', { name: /youtube music/i });
    expect(linkElement).toHaveAttribute('target', '_blank');
    expect(linkElement).toHaveAttribute('rel', 'noopener noreferrer');
  });

  it('handles multiple links of different types in sequence', () => {
    const { rerender, container } = render(
      <MusicServiceLink link={{ serviceType: MusicServiceType.Spotify, url: 'https://spotify.com/1' }} />
    );
    expect(screen.getByText('Spotify')).toBeVisible();

    rerender(
      <MusicServiceLink link={{ serviceType: MusicServiceType.AppleMusic, url: 'https://apple.com/2' }} />
    );
    expect(screen.getByText('Apple Music')).toBeVisible();
    expect(screen.queryByText('Spotify')).not.toBeInTheDocument();

    rerender(
      <MusicServiceLink link={{ serviceType: MusicServiceType.YouTubeMusic, url: 'https://youtube.com/3' }} />
    );
    expect(screen.getByText('YouTube Music')).toBeVisible();
    expect(screen.queryByText('Apple Music')).not.toBeInTheDocument();

    rerender(
      <MusicServiceLink link={{ serviceType: 999 as MusicServiceType, url: 'https://unknown.com/4' }} />
    );
    expect(container.innerHTML).toBe('');
  });

  it('applies correct brand styling for each service', () => {
    const { rerender } = render(
      <MusicServiceLink link={{ serviceType: MusicServiceType.Spotify, url: 'https://spotify.com/track/1' }} />
    );
    let link = screen.getByRole('link', { name: /spotify/i });
    expect(link).toHaveClass('bg-[#1ED760]');

    rerender(
      <MusicServiceLink link={{ serviceType: MusicServiceType.AppleMusic, url: 'https://apple.com/album/2' }} />
    );
    link = screen.getByRole('link', { name: /apple music/i });
    expect(link).toHaveClass('bg-black');

    rerender(
      <MusicServiceLink link={{ serviceType: MusicServiceType.YouTubeMusic, url: 'https://youtube.com/watch?v=3' }} />
    );
    link = screen.getByRole('link', { name: /youtube music/i });
    expect(link).toHaveClass('bg-black');
  });

  it('renders service icons correctly', () => {
    const { rerender } = render(
      <MusicServiceLink link={{ serviceType: MusicServiceType.Spotify, url: 'https://spotify.com/1' }} />
    );
    let icon = screen.getByAltText('Spotify');
    expect(icon).toHaveAttribute('src', '/Spotify_Icon.svg');

    rerender(
      <MusicServiceLink link={{ serviceType: MusicServiceType.AppleMusic, url: 'https://apple.com/2' }} />
    );
    icon = screen.getByAltText('Apple Music');
    expect(icon).toHaveAttribute('src', '/Apple_Music_Icon.svg');

    rerender(
      <MusicServiceLink link={{ serviceType: MusicServiceType.YouTubeMusic, url: 'https://youtube.com/3' }} />
    );
    icon = screen.getByAltText('YouTube Music');
    expect(icon).toHaveAttribute('src', '/YT_Music_Icon.svg');
  });
});
