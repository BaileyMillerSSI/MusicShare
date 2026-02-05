import { renderHook, act } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

// Mock lib/pwa so we control every utility the hook imports.
vi.mock('../lib/pwa', () => ({
  isIOS: vi.fn(() => false),
  isStandalone: vi.fn(() => false),
  isDismissed: vi.fn(() => false),
  setDismissed: vi.fn(),
}));

import { isIOS, isStandalone, isDismissed, setDismissed } from '../lib/pwa';
import { usePWAInstall } from './usePWAInstall';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Advance fake timers past the 5-second show-delay and flush microtasks. */
async function advancePastDelay() {
  await act(async () => {
    vi.advanceTimersByTime(5001);
  });
}

/** Fire a `beforeinstallprompt` event on window with a minimal mock prompt. */
function fireBeforeInstallPrompt(outcome: 'accepted' | 'dismissed' = 'accepted') {
  const event = new Event('beforeinstallprompt') as Event & {
    prompt: () => Promise<void>;
    userChoice: Promise<{ outcome: string }>;
  };
  event.preventDefault = vi.fn();
  event.prompt = vi.fn(() => Promise.resolve());
  event.userChoice = Promise.resolve({ outcome });
  window.dispatchEvent(event);
  return event;
}

/** Fire an `appinstalled` event on window. */
function fireAppInstalled() {
  window.dispatchEvent(new Event('appinstalled'));
}

// ---------------------------------------------------------------------------
// Test suite
// ---------------------------------------------------------------------------

describe('usePWAInstall', () => {
  beforeEach(() => {
    vi.useFakeTimers();

    // Reset all mocks to safe defaults before each test.
    vi.mocked(isIOS).mockReturnValue(false);
    vi.mocked(isStandalone).mockReturnValue(false);
    vi.mocked(isDismissed).mockReturnValue(false);
    vi.mocked(setDismissed).mockReset();
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  // -----------------------------------------------------------------------
  // Initial state / canInstall gating
  // -----------------------------------------------------------------------

  it('canInstall is false before the 5-second delay has elapsed', async () => {
    const { result } = renderHook(() => usePWAInstall());

    // Fire the prompt immediately so the only remaining gate is the delay.
    await act(async () => {
      fireBeforeInstallPrompt();
    });

    // Advance only 4 seconds -- not enough.
    await act(async () => {
      vi.advanceTimersByTime(4000);
    });

    expect(result.current.canInstall).toBe(false);
  });

  it('canInstall becomes true after 5-second delay when a prompt is available', async () => {
    const { result } = renderHook(() => usePWAInstall());

    await act(async () => {
      fireBeforeInstallPrompt();
    });

    await advancePastDelay();

    expect(result.current.canInstall).toBe(true);
  });

  it('canInstall is false when already installed (standalone)', () => {
    vi.mocked(isStandalone).mockReturnValue(true);

    const { result } = renderHook(() => usePWAInstall());

    expect(result.current.canInstall).toBe(false);
    expect(result.current.isInstalled).toBe(true);
  });

  it('canInstall is false when previously dismissed', async () => {
    vi.mocked(isDismissed).mockReturnValue(true);

    const { result } = renderHook(() => usePWAInstall());

    await act(async () => {
      fireBeforeInstallPrompt();
    });

    await advancePastDelay();

    expect(result.current.canInstall).toBe(false);
  });

  // -----------------------------------------------------------------------
  // iOS detection
  // -----------------------------------------------------------------------

  it('isIOSDevice reflects the value returned by isIOS()', () => {
    vi.mocked(isIOS).mockReturnValue(true);

    const { result } = renderHook(() => usePWAInstall());

    expect(result.current.isIOSDevice).toBe(true);
  });

  it('canInstall becomes true on iOS after delay even without a prompt event', async () => {
    vi.mocked(isIOS).mockReturnValue(true);

    const { result } = renderHook(() => usePWAInstall());

    // No beforeinstallprompt fired -- iOS does not support it.
    await advancePastDelay();

    expect(result.current.canInstall).toBe(true);
    expect(result.current.isIOSDevice).toBe(true);
  });

  // -----------------------------------------------------------------------
  // beforeinstallprompt event handling
  // -----------------------------------------------------------------------

  it('captures the deferred prompt when beforeinstallprompt fires', async () => {
    const { result } = renderHook(() => usePWAInstall());

    await act(async () => {
      fireBeforeInstallPrompt();
    });

    await advancePastDelay();

    // The hook does not expose deferredPrompt directly, but canInstall becoming
    // true (with isIOS=false) is proof the prompt was captured.
    expect(result.current.canInstall).toBe(true);
  });

  // -----------------------------------------------------------------------
  // appinstalled event handling
  // -----------------------------------------------------------------------

  it('sets isInstalled to true and canInstall to false when appinstalled fires', async () => {
    const { result } = renderHook(() => usePWAInstall());

    await act(async () => {
      fireBeforeInstallPrompt();
    });

    await advancePastDelay();
    expect(result.current.canInstall).toBe(true);

    await act(async () => {
      fireAppInstalled();
    });

    expect(result.current.isInstalled).toBe(true);
    expect(result.current.canInstall).toBe(false);
  });

  // -----------------------------------------------------------------------
  // promptInstall
  // -----------------------------------------------------------------------

  it('promptInstall calls prompt() on the deferred event', async () => {
    const { result } = renderHook(() => usePWAInstall());

    const event = await act(() => {
      return fireBeforeInstallPrompt('accepted');
    });

    await advancePastDelay();

    await act(async () => {
      await result.current.promptInstall();
    });

    expect(event.prompt).toHaveBeenCalledTimes(1);
  });

  it('promptInstall clears the prompt so canInstall becomes false afterward', async () => {
    const { result } = renderHook(() => usePWAInstall());

    await act(() => {
      fireBeforeInstallPrompt('accepted');
    });

    await advancePastDelay();
    expect(result.current.canInstall).toBe(true);

    await act(async () => {
      await result.current.promptInstall();
    });

    // Prompt is consumed; on non-iOS, canInstall should now be false.
    expect(result.current.canInstall).toBe(false);
  });

  it('promptInstall calls setDismissed when the user dismisses the native prompt', async () => {
    const { result } = renderHook(() => usePWAInstall());

    await act(() => {
      fireBeforeInstallPrompt('dismissed');
    });

    await advancePastDelay();

    await act(async () => {
      await result.current.promptInstall();
    });

    expect(setDismissed).toHaveBeenCalledTimes(1);
  });

  it('promptInstall does not call setDismissed when the user accepts the native prompt', async () => {
    const { result } = renderHook(() => usePWAInstall());

    await act(() => {
      fireBeforeInstallPrompt('accepted');
    });

    await advancePastDelay();

    await act(async () => {
      await result.current.promptInstall();
    });

    expect(setDismissed).not.toHaveBeenCalled();
  });

  it('promptInstall is a no-op when there is no deferred prompt', async () => {
    const { result } = renderHook(() => usePWAInstall());

    // Do not fire beforeinstallprompt. promptInstall should return without error.
    await act(async () => {
      await result.current.promptInstall();
    });

    expect(setDismissed).not.toHaveBeenCalled();
  });

  // -----------------------------------------------------------------------
  // dismissPrompt
  // -----------------------------------------------------------------------

  it('dismissPrompt persists the dismissal and sets canInstall to false', async () => {
    vi.mocked(isIOS).mockReturnValue(true);

    const { result } = renderHook(() => usePWAInstall());

    await advancePastDelay();
    expect(result.current.canInstall).toBe(true);

    await act(async () => {
      result.current.dismissPrompt();
    });

    expect(setDismissed).toHaveBeenCalledTimes(1);
    expect(result.current.canInstall).toBe(false);
  });
});
