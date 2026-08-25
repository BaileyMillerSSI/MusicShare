'use client';

import { useState, useEffect, useCallback } from 'react';
import { isIOS, isStandalone, isDismissed, setDismissed } from '../lib/pwa';

interface BeforeInstallPromptEvent extends Event {
  prompt(): Promise<void>;
  userChoice: Promise<{ outcome: 'accepted' | 'dismissed' }>;
}

interface UsePWAInstallReturn {
  canInstall: boolean;
  isInstalled: boolean;
  promptInstall: () => Promise<void>;
  dismissPrompt: () => void;
}

export function usePWAInstall(): UsePWAInstallReturn {
  const [deferredPrompt, setDeferredPrompt] =
    useState<BeforeInstallPromptEvent | null>(null);
  const [isInstalled, setIsInstalled] = useState(isStandalone);
  const [showDelayPassed, setShowDelayPassed] = useState(false);
  const [dismissed, setDismissedState] = useState(isDismissed);
  const [isIOSDevice] = useState(isIOS);

  // Delay before showing prompt
  useEffect(() => {
    if (isIOSDevice) return;

    const timer = setTimeout(() => {
      setShowDelayPassed(true);
    }, 5000);

    return () => clearTimeout(timer);
  }, [isIOSDevice]);

  // Listen for beforeinstallprompt event
  useEffect(() => {
    if (isIOSDevice) return;

    const handleBeforeInstallPrompt = (e: Event) => {
      e.preventDefault();
      setDeferredPrompt(e as BeforeInstallPromptEvent);
    };

    const handleAppInstalled = () => {
      setDeferredPrompt(null);
      setIsInstalled(true);
    };

    window.addEventListener('beforeinstallprompt', handleBeforeInstallPrompt);
    window.addEventListener('appinstalled', handleAppInstalled);

    return () => {
      window.removeEventListener(
        'beforeinstallprompt',
        handleBeforeInstallPrompt
      );
      window.removeEventListener('appinstalled', handleAppInstalled);
    };
  }, [isIOSDevice]);

  const promptInstall = useCallback(async () => {
    if (!deferredPrompt) return;

    deferredPrompt.prompt();
    const { outcome } = await deferredPrompt.userChoice;

    if (outcome === 'dismissed') {
      setDismissed();
      setDismissedState(true);
    }

    setDeferredPrompt(null);
  }, [deferredPrompt]);

  const dismissPrompt = useCallback(() => {
    setDismissed();
    setDismissedState(true);
  }, []);

  const canInstall =
    !isIOSDevice &&
    showDelayPassed &&
    !isInstalled &&
    !dismissed &&
    deferredPrompt !== null;

  return {
    canInstall,
    isInstalled,
    promptInstall,
    dismissPrompt,
  };
}
