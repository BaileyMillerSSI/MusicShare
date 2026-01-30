import type { ServiceLinkProps } from ".";
import Icon from "../../assets/Spotify_Icon.svg";
import BaseMusicLink from "./BaseMusicLink";

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
