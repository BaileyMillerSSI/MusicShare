import { MusicServiceType, type ServiceLink } from '../../lib/api';
import SpotifyLink from './SpotifyLink';
import AppleMusicLink from './AppleMusicLink';
import YouTubeMusicLink from './YouTubeMusicLink';

interface MusicServiceLinkProps {
  link: ServiceLink;
}

export default function MusicServiceLink({ link }: Readonly<MusicServiceLinkProps>) {
  switch (link.serviceType) {
    case MusicServiceType.Spotify:
      return <SpotifyLink url={link.url} />;
    case MusicServiceType.AppleMusic:
      return <AppleMusicLink url={link.url} />;
    case MusicServiceType.YouTubeMusic:
      return <YouTubeMusicLink url={link.url} />;
    default:
      return null;
  }
}
