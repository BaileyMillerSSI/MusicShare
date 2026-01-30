import type { ServiceLinkProps } from ".";
import Icon from "../../assets/YT_Music_Icon.svg";
import BaseMusicLink from "./BaseMusicLink";

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
