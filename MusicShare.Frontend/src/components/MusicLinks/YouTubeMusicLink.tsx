import type { ServiceLinkProps } from ".";
import BaseMusicLink from "./BaseMusicLink";

const Icon = "/YT_Music_Icon.svg";

export default function YouTubeMusicLink({ url }: Readonly<ServiceLinkProps>) {
    return (
        <BaseMusicLink
            url={url}
            service="YouTube Music"
            icon={Icon}
            color="bg-black"
        />
    );
}
