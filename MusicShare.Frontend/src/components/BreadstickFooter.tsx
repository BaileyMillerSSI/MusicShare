const breadstickLabsUrl =
  'https://breadsticklabs.com/?utm_source=musicshare&utm_medium=referral&utm_campaign=musicshare_footer';

export function BreadstickFooter() {
  return (
    <footer className="mt-6 text-center text-xs text-white/80">
      <a
        className="transition-colors hover:text-white hover:underline"
        href={breadstickLabsUrl}
        target="_blank"
        rel="noreferrer"
      >
        Proudly baked by Breadstick Labs
      </a>
    </footer>
  );
}
