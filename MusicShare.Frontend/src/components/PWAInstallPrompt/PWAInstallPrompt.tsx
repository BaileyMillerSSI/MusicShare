'use client';

import { usePWAInstall } from '../../hooks/usePWAInstall';
import IOSInstallInstructions from './IOSInstallInstructions';
import InstallBanner from './InstallBanner';

export function PWAInstallPrompt() {
  const { canInstall, isIOSDevice, promptInstall, dismissPrompt } =
    usePWAInstall();

  if (!canInstall) return null;

  if (isIOSDevice) {
    return <IOSInstallInstructions onDismiss={dismissPrompt} />;
  }

  return <InstallBanner onInstall={promptInstall} onDismiss={dismissPrompt} />;
}
