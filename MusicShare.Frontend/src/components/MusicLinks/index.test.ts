import { describe, it, expect } from 'vitest';
import * as MusicLinksExports from './index';

describe('MusicLinks index', () => {
  it('exports all components', () => {
    expect(MusicLinksExports.SpotifyLink).toBeDefined();
    expect(MusicLinksExports.AppleMusicLink).toBeDefined();
    expect(MusicLinksExports.YouTubeMusicLink).toBeDefined();
    expect(MusicLinksExports.MusicServiceLink).toBeDefined();
  });

  it('exports ServiceLinkProps type', () => {
    // Type-only export verification - if this compiles, the type exists
    type ServiceLinkPropsType = MusicLinksExports.ServiceLinkProps;
    const props: ServiceLinkPropsType = { url: 'https://example.com' };
    expect(props.url).toBe('https://example.com');
  });

  it('exports are functions (components)', () => {
    expect(typeof MusicLinksExports.SpotifyLink).toBe('function');
    expect(typeof MusicLinksExports.AppleMusicLink).toBe('function');
    expect(typeof MusicLinksExports.YouTubeMusicLink).toBe('function');
    expect(typeof MusicLinksExports.MusicServiceLink).toBe('function');
  });
});
