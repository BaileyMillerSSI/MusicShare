export const PWA_INSTALL_DISMISSED_KEY = 'pwa-install-dismissed';
export const PWA_INSTALL_DISMISSED_DURATION_DAYS = 180;

export function isIOS(): boolean {
  if (typeof window === 'undefined') return false;

  const userAgent = window.navigator.userAgent.toLowerCase();
  const isIOSDevice = /iphone|ipad|ipod/.test(userAgent);

  // iPad on iOS 13+ reports as Mac
  const isIPadOS =
    userAgent.includes('mac') && 'ontouchend' in document;

  return isIOSDevice || isIPadOS;
}

export function isStandalone(): boolean {
  if (typeof window === 'undefined') return false;

  // Check display-mode media query
  const standaloneQuery = window.matchMedia('(display-mode: standalone)');
  if (standaloneQuery.matches) return true;

  // Check iOS Safari standalone mode
  if ('standalone' in window.navigator) {
    return (window.navigator as Navigator & { standalone: boolean }).standalone;
  }

  return false;
}

export function isDismissed(): boolean {
  if (typeof window === 'undefined') return false;

  const dismissed = localStorage.getItem(PWA_INSTALL_DISMISSED_KEY);
  if (!dismissed) return false;

  const dismissedTime = parseInt(dismissed, 10);
  if (isNaN(dismissedTime)) return false;

  const now = Date.now();
  const daysSinceDismissal =
    (now - dismissedTime) / (1000 * 60 * 60 * 24);

  return daysSinceDismissal < PWA_INSTALL_DISMISSED_DURATION_DAYS;
}

export function setDismissed(): void {
  if (typeof window === 'undefined') return;
  localStorage.setItem(PWA_INSTALL_DISMISSED_KEY, Date.now().toString());
}
