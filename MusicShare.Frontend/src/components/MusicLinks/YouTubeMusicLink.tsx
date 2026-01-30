import type { ServiceLinkProps } from ".";

export default function YouTubeMusicLink({ url }: Readonly<ServiceLinkProps>) {
  return (<a
    href={url}
    target="_blank"
    rel="noopener noreferrer"
    className="flex items-center justify-between bg-[#FF0033] text-white py-4 px-5 rounded-lg transition-all hover:shadow-lg transform hover:-translate-y-0.5"
  >
    <div className="flex items-center gap-3">
      <svg className="w-10 h-10" viewBox="0 0 24 24" fill="currentColor">
        <path d="M12 0C5.376 0 0 5.376 0 12s5.376 12 12 12 12-5.376 12-12S18.624 0 12 0zm0 19.104c-3.924 0-7.104-3.18-7.104-7.104S8.076 4.896 12 4.896s7.104 3.18 7.104 7.104-3.18 7.104-7.104 7.104zm0-13.332c-3.432 0-6.228 2.796-6.228 6.228S8.568 18.228 12 18.228s6.228-2.796 6.228-6.228S15.432 5.772 12 5.772zM9.684 15.54V8.46L15.816 12l-6.132 3.54z"/>
      </svg>
      <div className="flex flex-col items-start">
        <span className="text-xs">Listen on</span>
        <span className="text-sm font-semibold">YouTube Music</span>
      </div>
    </div>
    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 5l7 7-7 7" />
    </svg>
  </a>)
}
