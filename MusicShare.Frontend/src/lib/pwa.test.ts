import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import {
  isIOS,
  isStandalone,
  isDismissed,
  setDismissed,
  PWA_INSTALL_DISMISSED_KEY,
  PWA_INSTALL_DISMISSED_DURATION_DAYS,
} from './pwa';

describe('PWA Utilities', () => {
  describe('Constants', () => {
    it('exports correct localStorage key', () => {
      expect(PWA_INSTALL_DISMISSED_KEY).toBe('pwa-install-dismissed');
    });

    it('exports correct dismissal duration', () => {
      expect(PWA_INSTALL_DISMISSED_DURATION_DAYS).toBe(30);
    });
  });

  describe('isIOS', () => {
    let originalNavigator: Navigator;
    let originalDocument: Document;

    beforeEach(() => {
      originalNavigator = window.navigator;
      originalDocument = window.document;
    });

    afterEach(() => {
      Object.defineProperty(window, 'navigator', {
        value: originalNavigator,
        writable: true,
        configurable: true,
      });
      Object.defineProperty(window, 'document', {
        value: originalDocument,
        writable: true,
        configurable: true,
      });
    });

    describe('Server-Side Rendering', () => {
      it('returns false when window is undefined', () => {
        const originalWindow = global.window;
        // @ts-expect-error: Testing SSR scenario
        delete global.window;

        const result = isIOS();

        expect(result).toBe(false);

        global.window = originalWindow;
      });
    });

    describe('iOS Device Detection', () => {
      it('returns true for iPhone', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (iPhone; CPU iPhone OS 14_0 like Mac OS X)',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(true);
      });

      it('returns true for iPad', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (iPad; CPU OS 14_0 like Mac OS X)',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(true);
      });

      it('returns true for iPod', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (iPod touch; CPU iPhone OS 12_0 like Mac OS X)',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(true);
      });

      it('handles case-insensitive user agent', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (IPHONE; CPU iPhone OS 14_0 like Mac OS X)',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(true);
      });
    });

    describe('iPadOS 13+ Detection', () => {
      it('returns true for iPad on iOS 13+ (reports as Mac)', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15)',
          writable: true,
          configurable: true,
        });
        Object.defineProperty(window.document, 'ontouchend', {
          value: true,
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(true);
      });

      it('returns false for real Mac without touch', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15)',
          writable: true,
          configurable: true,
        });
        // @ts-expect-error: Testing absence of property
        delete window.document.ontouchend;

        const result = isIOS();

        expect(result).toBe(false);
      });

      it('returns false for Mac user agent without touch support', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (Macintosh; Intel Mac OS X 11_2_3)',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(false);
      });
    });

    describe('Non-iOS Devices', () => {
      it('returns false for Android', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (Linux; Android 11; SM-G991B)',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(false);
      });

      it('returns false for Windows', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(false);
      });

      it('returns false for Linux', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (X11; Linux x86_64)',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(false);
      });

      it('returns false for Chrome OS', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (X11; CrOS x86_64 13904.97.0)',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(false);
      });
    });

    describe('Edge Cases', () => {
      it('handles empty user agent', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: '',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(false);
      });

      it('handles malformed user agent', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'InvalidUserAgent12345',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        expect(result).toBe(false);
      });

      it('detects iOS substring in user agent string', () => {
        Object.defineProperty(window.navigator, 'userAgent', {
          value: 'Mozilla/5.0 (Windows NT; iPhone in string)',
          writable: true,
          configurable: true,
        });

        const result = isIOS();

        // The regex /iphone|ipad|ipod/ will match "iphone" in the string
        // This is expected behavior - it's a simple substring match
        expect(result).toBe(true);
      });
    });
  });

  describe('isStandalone', () => {
    let originalNavigator: Navigator;

    beforeEach(() => {
      originalNavigator = window.navigator;
    });

    afterEach(() => {
      Object.defineProperty(window, 'navigator', {
        value: originalNavigator,
        writable: true,
        configurable: true,
      });
      vi.restoreAllMocks();
    });

    describe('Server-Side Rendering', () => {
      it('returns false when window is undefined', () => {
        const originalWindow = global.window;
        // @ts-expect-error: Testing SSR scenario
        delete global.window;

        const result = isStandalone();

        expect(result).toBe(false);

        global.window = originalWindow;
      });
    });

    describe('Display Mode Media Query', () => {
      it('returns true when display-mode is standalone', () => {
        const mockMatchMedia = vi.fn().mockReturnValue({
          matches: true,
          media: '(display-mode: standalone)',
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
        });
        window.matchMedia = mockMatchMedia;

        const result = isStandalone();

        expect(result).toBe(true);
        expect(mockMatchMedia).toHaveBeenCalledWith('(display-mode: standalone)');
      });

      it('returns false when display-mode is not standalone', () => {
        const mockMatchMedia = vi.fn().mockReturnValue({
          matches: false,
          media: '(display-mode: browser)',
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
        });
        window.matchMedia = mockMatchMedia;
        // Make sure standalone property does not exist
        // @ts-expect-error: Testing absence of property
        delete window.navigator.standalone;

        const result = isStandalone();

        expect(result).toBe(false);
      });

      it('handles display-mode fullscreen as non-standalone', () => {
        const mockMatchMedia = vi.fn().mockReturnValue({
          matches: false,
          media: '(display-mode: fullscreen)',
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
        });
        window.matchMedia = mockMatchMedia;
        // Make sure standalone property does not exist
        // @ts-expect-error: Testing absence of property
        delete window.navigator.standalone;

        const result = isStandalone();

        expect(result).toBe(false);
      });

      it('handles display-mode minimal-ui as non-standalone', () => {
        const mockMatchMedia = vi.fn().mockReturnValue({
          matches: false,
          media: '(display-mode: minimal-ui)',
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
        });
        window.matchMedia = mockMatchMedia;
        // Make sure standalone property does not exist
        // @ts-expect-error: Testing absence of property
        delete window.navigator.standalone;

        const result = isStandalone();

        expect(result).toBe(false);
      });
    });

    describe('iOS Safari Standalone Mode', () => {
      it('returns true when iOS Safari standalone is true', () => {
        const mockMatchMedia = vi.fn().mockReturnValue({
          matches: false,
          media: '(display-mode: browser)',
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
        });
        window.matchMedia = mockMatchMedia;
        Object.defineProperty(window.navigator, 'standalone', {
          value: true,
          writable: true,
          configurable: true,
        });

        const result = isStandalone();

        expect(result).toBe(true);
      });

      it('returns false when iOS Safari standalone is false', () => {
        const mockMatchMedia = vi.fn().mockReturnValue({
          matches: false,
          media: '(display-mode: browser)',
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
        });
        window.matchMedia = mockMatchMedia;
        Object.defineProperty(window.navigator, 'standalone', {
          value: false,
          writable: true,
          configurable: true,
        });

        const result = isStandalone();

        expect(result).toBe(false);
      });

      it('returns false when standalone property does not exist', () => {
        const mockMatchMedia = vi.fn().mockReturnValue({
          matches: false,
          media: '(display-mode: browser)',
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
        });
        window.matchMedia = mockMatchMedia;
        // @ts-expect-error: Testing absence of property
        delete window.navigator.standalone;

        const result = isStandalone();

        expect(result).toBe(false);
      });
    });

    describe('Priority', () => {
      it('prioritizes display-mode media query over iOS standalone', () => {
        const mockMatchMedia = vi.fn().mockReturnValue({
          matches: true,
          media: '(display-mode: standalone)',
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
        });
        window.matchMedia = mockMatchMedia;
        Object.defineProperty(window.navigator, 'standalone', {
          value: false,
          writable: true,
          configurable: true,
        });

        const result = isStandalone();

        expect(result).toBe(true);
      });
    });

    describe('Browser Compatibility', () => {
      it('handles missing matchMedia support', () => {
        // @ts-expect-error: Testing missing API
        delete window.matchMedia;
        Object.defineProperty(window.navigator, 'standalone', {
          value: false,
          writable: true,
          configurable: true,
        });

        // This will throw, so we need to handle it
        expect(() => isStandalone()).toThrow();
      });
    });
  });

  describe('isDismissed', () => {
    let originalLocalStorage: Storage;

    beforeEach(() => {
      originalLocalStorage = window.localStorage;
      const mockLocalStorage: Record<string, string> = {};
      Object.defineProperty(window, 'localStorage', {
        value: {
          getItem: vi.fn((key: string) => mockLocalStorage[key] || null),
          setItem: vi.fn((key: string, value: string) => {
            mockLocalStorage[key] = value;
          }),
          removeItem: vi.fn((key: string) => {
            delete mockLocalStorage[key];
          }),
          clear: vi.fn(() => {
            Object.keys(mockLocalStorage).forEach((key) => {
              delete mockLocalStorage[key];
            });
          }),
        } as Storage,
        writable: true,
        configurable: true,
      });
      vi.clearAllTimers();
    });

    afterEach(() => {
      Object.defineProperty(window, 'localStorage', {
        value: originalLocalStorage,
        writable: true,
        configurable: true,
      });
      vi.useRealTimers();
    });

    describe('Server-Side Rendering', () => {
      it('returns false when window is undefined', () => {
        const originalWindow = global.window;
        // @ts-expect-error: Testing SSR scenario
        delete global.window;

        const result = isDismissed();

        expect(result).toBe(false);

        global.window = originalWindow;
      });
    });

    describe('No Dismissal', () => {
      it('returns false when no dismissal is stored', () => {
        const result = isDismissed();

        expect(result).toBe(false);
        expect(localStorage.getItem).toHaveBeenCalledWith(PWA_INSTALL_DISMISSED_KEY);
      });

      it('returns false when localStorage returns null', () => {
        vi.mocked(localStorage.getItem).mockReturnValue(null);

        const result = isDismissed();

        expect(result).toBe(false);
      });
    });

    describe('Valid Dismissal', () => {
      it('returns true when dismissed within duration window', () => {
        const now = Date.now();
        vi.mocked(localStorage.getItem).mockReturnValue(now.toString());
        vi.spyOn(Date, 'now').mockReturnValue(now);

        const result = isDismissed();

        expect(result).toBe(true);
      });

      it('returns true when dismissed 1 day ago', () => {
        const oneDayAgo = Date.now() - 24 * 60 * 60 * 1000;
        vi.mocked(localStorage.getItem).mockReturnValue(oneDayAgo.toString());

        const result = isDismissed();

        expect(result).toBe(true);
      });

      it('returns true when dismissed 15 days ago (halfway)', () => {
        const fifteenDaysAgo = Date.now() - 15 * 24 * 60 * 60 * 1000;
        vi.mocked(localStorage.getItem).mockReturnValue(fifteenDaysAgo.toString());

        const result = isDismissed();

        expect(result).toBe(true);
      });

      it('returns true when dismissed 29 days ago (just before expiry)', () => {
        const twentyNineDaysAgo = Date.now() - 29 * 24 * 60 * 60 * 1000;
        vi.mocked(localStorage.getItem).mockReturnValue(twentyNineDaysAgo.toString());

        const result = isDismissed();

        expect(result).toBe(true);
      });

      it('returns true when dismissed exactly at boundary (29.99 days)', () => {
        const almostThirtyDays = Date.now() - (30 * 24 * 60 * 60 * 1000 - 1000);
        vi.mocked(localStorage.getItem).mockReturnValue(almostThirtyDays.toString());

        const result = isDismissed();

        expect(result).toBe(true);
      });
    });

    describe('Expired Dismissal', () => {
      it('returns false when dismissed exactly 30 days ago', () => {
        const thirtyDaysAgo = Date.now() - 30 * 24 * 60 * 60 * 1000;
        vi.mocked(localStorage.getItem).mockReturnValue(thirtyDaysAgo.toString());

        const result = isDismissed();

        expect(result).toBe(false);
      });

      it('returns false when dismissed 31 days ago', () => {
        const thirtyOneDaysAgo = Date.now() - 31 * 24 * 60 * 60 * 1000;
        vi.mocked(localStorage.getItem).mockReturnValue(thirtyOneDaysAgo.toString());

        const result = isDismissed();

        expect(result).toBe(false);
      });

      it('returns false when dismissed 60 days ago', () => {
        const sixtyDaysAgo = Date.now() - 60 * 24 * 60 * 60 * 1000;
        vi.mocked(localStorage.getItem).mockReturnValue(sixtyDaysAgo.toString());

        const result = isDismissed();

        expect(result).toBe(false);
      });

      it('returns false when dismissed 365 days ago', () => {
        const oneYearAgo = Date.now() - 365 * 24 * 60 * 60 * 1000;
        vi.mocked(localStorage.getItem).mockReturnValue(oneYearAgo.toString());

        const result = isDismissed();

        expect(result).toBe(false);
      });
    });

    describe('Invalid Values', () => {
      it('returns false when localStorage value is not a number', () => {
        vi.mocked(localStorage.getItem).mockReturnValue('not-a-number');

        const result = isDismissed();

        expect(result).toBe(false);
      });

      it('returns false when localStorage value is empty string', () => {
        vi.mocked(localStorage.getItem).mockReturnValue('');

        const result = isDismissed();

        expect(result).toBe(false);
      });

      it('returns false when localStorage value is NaN', () => {
        vi.mocked(localStorage.getItem).mockReturnValue('NaN');

        const result = isDismissed();

        expect(result).toBe(false);
      });

      it('returns false when localStorage value is Infinity', () => {
        vi.mocked(localStorage.getItem).mockReturnValue('Infinity');

        const result = isDismissed();

        expect(result).toBe(false);
      });

      it('returns false when localStorage value is negative number', () => {
        vi.mocked(localStorage.getItem).mockReturnValue('-12345');

        const result = isDismissed();

        expect(result).toBe(false);
      });

      it('returns false when localStorage value is float string', () => {
        vi.mocked(localStorage.getItem).mockReturnValue('123.456.789');

        const result = isDismissed();

        expect(result).toBe(false);
      });

      it('returns false when localStorage value contains letters', () => {
        vi.mocked(localStorage.getItem).mockReturnValue('123abc');

        const result = isDismissed();

        expect(result).toBe(false);
      });

      it('returns false when localStorage value is JSON object', () => {
        vi.mocked(localStorage.getItem).mockReturnValue('{"time": 123}');

        const result = isDismissed();

        expect(result).toBe(false);
      });
    });

    describe('Edge Cases', () => {
      it('handles very large timestamp (future date)', () => {
        const futureDate = Date.now() + 365 * 24 * 60 * 60 * 1000;
        vi.mocked(localStorage.getItem).mockReturnValue(futureDate.toString());

        const result = isDismissed();

        // Future date means negative days since dismissal, which is < 30
        expect(result).toBe(true);
      });

      it('handles timestamp of 0 (epoch)', () => {
        vi.mocked(localStorage.getItem).mockReturnValue('0');

        const result = isDismissed();

        // This is many years ago, well beyond 30 days
        expect(result).toBe(false);
      });

      it('handles very old timestamp', () => {
        vi.mocked(localStorage.getItem).mockReturnValue('1000000000000'); // Sep 2001

        const result = isDismissed();

        expect(result).toBe(false);
      });

      it('handles timestamp with decimal (valid)', () => {
        const now = Date.now();
        vi.mocked(localStorage.getItem).mockReturnValue(`${now}.5`);
        vi.spyOn(Date, 'now').mockReturnValue(now);

        const result = isDismissed();

        // parseInt will ignore the .5 and use the integer part
        expect(result).toBe(true);
      });

      it('handles scientific notation', () => {
        vi.mocked(localStorage.getItem).mockReturnValue('1e12');

        const result = isDismissed();

        // parseInt cannot parse scientific notation, returns NaN
        expect(result).toBe(false);
      });
    });

    describe('Time Calculation', () => {
      it('correctly calculates days since dismissal', () => {
        const now = Date.now();
        const sevenDaysAgo = now - 7 * 24 * 60 * 60 * 1000;
        vi.mocked(localStorage.getItem).mockReturnValue(sevenDaysAgo.toString());
        vi.spyOn(Date, 'now').mockReturnValue(now);

        const result = isDismissed();

        expect(result).toBe(true);
      });

      it('handles dismissal with partial days', () => {
        const now = Date.now();
        const partialDay = now - (7.5 * 24 * 60 * 60 * 1000);
        vi.mocked(localStorage.getItem).mockReturnValue(partialDay.toString());
        vi.spyOn(Date, 'now').mockReturnValue(now);

        const result = isDismissed();

        expect(result).toBe(true);
      });

      it('handles dismissal with hours and minutes', () => {
        const now = Date.now();
        const twelveHoursAgo = now - (12 * 60 * 60 * 1000);
        vi.mocked(localStorage.getItem).mockReturnValue(twelveHoursAgo.toString());
        vi.spyOn(Date, 'now').mockReturnValue(now);

        const result = isDismissed();

        expect(result).toBe(true);
      });
    });
  });

  describe('setDismissed', () => {
    let originalLocalStorage: Storage;

    beforeEach(() => {
      originalLocalStorage = window.localStorage;
      const mockLocalStorage: Record<string, string> = {};
      Object.defineProperty(window, 'localStorage', {
        value: {
          getItem: vi.fn((key: string) => mockLocalStorage[key] || null),
          setItem: vi.fn((key: string, value: string) => {
            mockLocalStorage[key] = value;
          }),
          removeItem: vi.fn((key: string) => {
            delete mockLocalStorage[key];
          }),
          clear: vi.fn(() => {
            Object.keys(mockLocalStorage).forEach((key) => {
              delete mockLocalStorage[key];
            });
          }),
        } as Storage,
        writable: true,
        configurable: true,
      });
    });

    afterEach(() => {
      Object.defineProperty(window, 'localStorage', {
        value: originalLocalStorage,
        writable: true,
        configurable: true,
      });
      vi.useRealTimers();
    });

    describe('Server-Side Rendering', () => {
      it('does not throw when window is undefined', () => {
        const originalWindow = global.window;
        // @ts-expect-error: Testing SSR scenario
        delete global.window;

        expect(() => setDismissed()).not.toThrow();

        global.window = originalWindow;
      });

      it('does nothing when window is undefined', () => {
        const originalWindow = global.window;
        const setItemSpy = vi.spyOn(window.localStorage, 'setItem');
        // @ts-expect-error: Testing SSR scenario
        delete global.window;

        setDismissed();

        expect(setItemSpy).not.toHaveBeenCalled();

        global.window = originalWindow;
      });
    });

    describe('Setting Dismissal', () => {
      it('stores current timestamp in localStorage', () => {
        const now = Date.now();
        vi.spyOn(Date, 'now').mockReturnValue(now);

        setDismissed();

        expect(localStorage.setItem).toHaveBeenCalledWith(
          PWA_INSTALL_DISMISSED_KEY,
          now.toString()
        );
      });

      it('uses correct localStorage key', () => {
        setDismissed();

        expect(localStorage.setItem).toHaveBeenCalledWith(
          'pwa-install-dismissed',
          expect.any(String)
        );
      });

      it('stores timestamp as string', () => {
        const now = 1234567890123;
        vi.spyOn(Date, 'now').mockReturnValue(now);

        setDismissed();

        const setItemCall = vi.mocked(localStorage.setItem).mock.calls[0];
        expect(setItemCall[1]).toBe('1234567890123');
        expect(typeof setItemCall[1]).toBe('string');
      });

      it('can be called multiple times', () => {
        const firstTime = 1000000000000;
        const secondTime = 2000000000000;

        vi.spyOn(Date, 'now').mockReturnValue(firstTime);
        setDismissed();
        expect(localStorage.setItem).toHaveBeenCalledWith(
          PWA_INSTALL_DISMISSED_KEY,
          firstTime.toString()
        );

        vi.spyOn(Date, 'now').mockReturnValue(secondTime);
        setDismissed();
        expect(localStorage.setItem).toHaveBeenCalledWith(
          PWA_INSTALL_DISMISSED_KEY,
          secondTime.toString()
        );

        expect(localStorage.setItem).toHaveBeenCalledTimes(2);
      });

      it('overwrites previous dismissal', () => {
        const firstTime = Date.now() - 1000;
        const secondTime = Date.now();

        vi.spyOn(Date, 'now').mockReturnValue(firstTime);
        setDismissed();

        vi.spyOn(Date, 'now').mockReturnValue(secondTime);
        setDismissed();

        const calls = vi.mocked(localStorage.setItem).mock.calls;
        expect(calls[0][1]).toBe(firstTime.toString());
        expect(calls[1][1]).toBe(secondTime.toString());
      });
    });

    describe('Integration with isDismissed', () => {
      it('makes isDismissed return true immediately after setDismissed', () => {
        const mockLocalStorage: Record<string, string> = {};
        Object.defineProperty(window, 'localStorage', {
          value: {
            getItem: vi.fn((key: string) => mockLocalStorage[key] || null),
            setItem: vi.fn((key: string, value: string) => {
              mockLocalStorage[key] = value;
            }),
            removeItem: vi.fn((key: string) => {
              delete mockLocalStorage[key];
            }),
            clear: vi.fn(),
          } as Storage,
          writable: true,
          configurable: true,
        });

        const now = Date.now();
        vi.spyOn(Date, 'now').mockReturnValue(now);

        expect(isDismissed()).toBe(false);

        setDismissed();

        expect(isDismissed()).toBe(true);
      });

      it('respects the 30-day expiration', () => {
        const mockLocalStorage: Record<string, string> = {};
        Object.defineProperty(window, 'localStorage', {
          value: {
            getItem: vi.fn((key: string) => mockLocalStorage[key] || null),
            setItem: vi.fn((key: string, value: string) => {
              mockLocalStorage[key] = value;
            }),
            removeItem: vi.fn((key: string) => {
              delete mockLocalStorage[key];
            }),
            clear: vi.fn(),
          } as Storage,
          writable: true,
          configurable: true,
        });

        const originalTime = Date.now();
        vi.spyOn(Date, 'now').mockReturnValue(originalTime);

        setDismissed();
        expect(isDismissed()).toBe(true);

        // Fast forward 31 days
        const thirtyOneDaysLater = originalTime + 31 * 24 * 60 * 60 * 1000;
        vi.spyOn(Date, 'now').mockReturnValue(thirtyOneDaysLater);

        expect(isDismissed()).toBe(false);
      });
    });

    describe('Edge Cases', () => {
      it('handles localStorage being full', () => {
        vi.mocked(localStorage.setItem).mockImplementation(() => {
          throw new Error('QuotaExceededError');
        });

        expect(() => setDismissed()).toThrow('QuotaExceededError');
      });

      it('handles localStorage being disabled', () => {
        Object.defineProperty(window, 'localStorage', {
          get() {
            throw new Error('localStorage is not available');
          },
          configurable: true,
        });

        expect(() => setDismissed()).toThrow('localStorage is not available');
      });

      it('handles Date.now returning different timestamps', () => {
        let callCount = 0;
        vi.spyOn(Date, 'now').mockImplementation(() => {
          callCount++;
          return 1000000000000 + callCount;
        });

        setDismissed();
        const firstCall = vi.mocked(localStorage.setItem).mock.calls[0][1];

        setDismissed();
        const secondCall = vi.mocked(localStorage.setItem).mock.calls[1][1];

        expect(firstCall).not.toBe(secondCall);
      });
    });

    describe('Type Safety', () => {
      it('always stores a string value', () => {
        const now = Date.now();
        vi.spyOn(Date, 'now').mockReturnValue(now);

        setDismissed();

        const storedValue = vi.mocked(localStorage.setItem).mock.calls[0][1];
        expect(typeof storedValue).toBe('string');
      });

      it('stores a valid number string that can be parsed', () => {
        const now = Date.now();
        vi.spyOn(Date, 'now').mockReturnValue(now);

        setDismissed();

        const storedValue = vi.mocked(localStorage.setItem).mock.calls[0][1];
        const parsed = parseInt(storedValue, 10);
        expect(isNaN(parsed)).toBe(false);
        expect(parsed).toBe(now);
      });
    });

    describe('Return Value', () => {
      it('returns undefined', () => {
        const result = setDismissed();

        expect(result).toBeUndefined();
      });
    });
  });

  describe('End-to-End Scenarios', () => {
    let originalLocalStorage: Storage;

    beforeEach(() => {
      originalLocalStorage = window.localStorage;
      const mockLocalStorage: Record<string, string> = {};
      Object.defineProperty(window, 'localStorage', {
        value: {
          getItem: vi.fn((key: string) => mockLocalStorage[key] || null),
          setItem: vi.fn((key: string, value: string) => {
            mockLocalStorage[key] = value;
          }),
          removeItem: vi.fn((key: string) => {
            delete mockLocalStorage[key];
          }),
          clear: vi.fn(() => {
            Object.keys(mockLocalStorage).forEach((key) => {
              delete mockLocalStorage[key];
            });
          }),
        } as Storage,
        writable: true,
        configurable: true,
      });
    });

    afterEach(() => {
      Object.defineProperty(window, 'localStorage', {
        value: originalLocalStorage,
        writable: true,
        configurable: true,
      });
      vi.useRealTimers();
    });

    it('complete flow: check, dismiss, check again', () => {
      const mockLocalStorage: Record<string, string> = {};
      Object.defineProperty(window, 'localStorage', {
        value: {
          getItem: vi.fn((key: string) => mockLocalStorage[key] || null),
          setItem: vi.fn((key: string, value: string) => {
            mockLocalStorage[key] = value;
          }),
          removeItem: vi.fn((key: string) => {
            delete mockLocalStorage[key];
          }),
          clear: vi.fn(),
        } as Storage,
        writable: true,
        configurable: true,
      });

      // Initially not dismissed
      expect(isDismissed()).toBe(false);

      // User dismisses
      setDismissed();

      // Now it's dismissed
      expect(isDismissed()).toBe(true);
    });

    it('complete flow: dismiss, wait 31 days, check again', () => {
      const mockLocalStorage: Record<string, string> = {};
      Object.defineProperty(window, 'localStorage', {
        value: {
          getItem: vi.fn((key: string) => mockLocalStorage[key] || null),
          setItem: vi.fn((key: string, value: string) => {
            mockLocalStorage[key] = value;
          }),
          removeItem: vi.fn((key: string) => {
            delete mockLocalStorage[key];
          }),
          clear: vi.fn(),
        } as Storage,
        writable: true,
        configurable: true,
      });

      const initialTime = Date.now();
      vi.spyOn(Date, 'now').mockReturnValue(initialTime);

      setDismissed();
      expect(isDismissed()).toBe(true);

      // Fast forward 31 days
      const laterTime = initialTime + 31 * 24 * 60 * 60 * 1000;
      vi.spyOn(Date, 'now').mockReturnValue(laterTime);

      expect(isDismissed()).toBe(false);
    });

    it('dismiss twice within 30 days updates timestamp', () => {
      const mockLocalStorage: Record<string, string> = {};
      Object.defineProperty(window, 'localStorage', {
        value: {
          getItem: vi.fn((key: string) => mockLocalStorage[key] || null),
          setItem: vi.fn((key: string, value: string) => {
            mockLocalStorage[key] = value;
          }),
          removeItem: vi.fn((key: string) => {
            delete mockLocalStorage[key];
          }),
          clear: vi.fn(),
        } as Storage,
        writable: true,
        configurable: true,
      });

      const firstTime = Date.now();
      vi.spyOn(Date, 'now').mockReturnValue(firstTime);
      setDismissed();

      // Fast forward 20 days
      const secondTime = firstTime + 20 * 24 * 60 * 60 * 1000;
      vi.spyOn(Date, 'now').mockReturnValue(secondTime);
      expect(isDismissed()).toBe(true);

      // Dismiss again
      setDismissed();

      // Fast forward another 15 days (35 days from first dismissal, 15 from second)
      const thirdTime = firstTime + 35 * 24 * 60 * 60 * 1000;
      vi.spyOn(Date, 'now').mockReturnValue(thirdTime);

      // Should still be dismissed because it's only 15 days from second dismissal
      expect(isDismissed()).toBe(true);
    });
  });
});
