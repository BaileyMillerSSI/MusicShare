'use client';

import { usePWAInstall } from '../../hooks/usePWAInstall';
import InstallBanner from './InstallBanner';

export function PWAInstallPrompt() {
  const { canInstall, promptInstall, dismissPrompt } = usePWAInstall();

  if (!canInstall) return null;

  return <InstallBanner onInstall={promptInstall} onDismiss={dismissPrompt} />;
}
