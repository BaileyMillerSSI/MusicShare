type Props = {
    url: string;
    service: string;
    icon: string;
    color: string;
};

export default function BaseMusicLink({
    url,
    service,
    icon,
    color,
}: Readonly<Props>) {
    return (
        <a
            href={url}
            target="_blank"
            rel="noopener noreferrer"
            className={`flex items-center justify-between text-white py-4 px-5 rounded-lg transition-all hover:shadow-lg transform hover:-translate-y-0.5 ${color}`}
        >
            <div className="flex items-center gap-3">
                <img src={icon} alt={service} className="w-10 h-10" />
                <div className="flex flex-col items-start">
                    <span className="text-xs">Listen on</span>
                    <span className="text-sm font-semibold">{service}</span>
                </div>
            </div>
            <svg
                className="w-5 h-5"
                fill="none"
                stroke="currentColor"
                viewBox="0 0 24 24"
            >
                <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth={2}
                    d="M9 5l7 7-7 7"
                />
            </svg>
        </a>
    );
}
