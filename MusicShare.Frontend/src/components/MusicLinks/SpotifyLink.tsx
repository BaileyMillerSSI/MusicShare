import type { ServiceLinkProps } from ".";
import BaseMusicLink from "./BaseMusicLink";

const Icon = "/Spotify_Icon.svg";

export default function SpotifyLink({ url }: Readonly<ServiceLinkProps>) {
    return (
        <BaseMusicLink
            url={url}
            service="Spotify"
            icon={Icon}
            color="bg-[#1ED760]"
        />
    );
}
