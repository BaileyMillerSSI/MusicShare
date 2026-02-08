import { describe, it, expect } from 'vitest';
import { formatDuration, durationToSeconds } from './utils';

describe('formatDuration', () => {
  describe('TimeSpan format (HH:MM:SS)', () => {
    it('formats 3 minutes 30 seconds', () => {
      expect(formatDuration('00:03:30')).toBe('3:30');
    });

    it('formats less than 1 minute', () => {
      expect(formatDuration('00:00:45')).toBe('0:45');
    });

    it('formats exactly 1 minute', () => {
      expect(formatDuration('00:01:00')).toBe('1:00');
    });

    it('formats hours, minutes, and seconds', () => {
      expect(formatDuration('01:23:45')).toBe('1:23:45');
    });

    it('formats just seconds', () => {
      expect(formatDuration('00:00:05')).toBe('0:05');
    });

    it('handles single-digit hours', () => {
      expect(formatDuration('2:15:30')).toBe('2:15:30');
    });

    it('handles .NET TimeSpan format with fractional seconds', () => {
      expect(formatDuration('00:03:45.9800000')).toBe('3:45');
    });

    it('handles fractional seconds in minutes:seconds format', () => {
      expect(formatDuration('03:45.5')).toBe('3:45');
    });
  });

  describe('ISO 8601 duration format', () => {
    it('formats PT3M30S as 3:30', () => {
      expect(formatDuration('PT3M30S')).toBe('3:30');
    });

    it('formats PT45S as 0:45', () => {
      expect(formatDuration('PT45S')).toBe('0:45');
    });

    it('formats PT1H23M45S as 1:23:45', () => {
      expect(formatDuration('PT1H23M45S')).toBe('1:23:45');
    });

    it('formats PT1M as 1:00', () => {
      expect(formatDuration('PT1M')).toBe('1:00');
    });

    it('formats PT1H as 1:00:00', () => {
      expect(formatDuration('PT1H')).toBe('1:00:00');
    });

    it('returns null for malformed ISO 8601', () => {
      expect(formatDuration('PT')).toBeNull();
      expect(formatDuration('PTXYZ')).toBeNull();
    });
  });

  describe('Numeric input (seconds)', () => {
    it('formats 210 seconds as 3:30', () => {
      expect(formatDuration(210)).toBe('3:30');
    });

    it('formats 45 seconds', () => {
      expect(formatDuration(45)).toBe('0:45');
    });

    it('formats 3600 seconds as 1:00:00', () => {
      expect(formatDuration(3600)).toBe('1:00:00');
    });

    it('formats 5005 seconds', () => {
      expect(formatDuration(5005)).toBe('1:23:25');
    });
  });

  describe('Numeric string input', () => {
    it('formats numeric string "210" as 3:30', () => {
      expect(formatDuration('210')).toBe('3:30');
    });

    it('formats decimal string "210.5" and floors it', () => {
      expect(formatDuration('210.5')).toBe('3:30');
    });
  });

  describe('Edge cases', () => {
    it('returns null for undefined', () => {
      expect(formatDuration(undefined)).toBeNull();
    });

    it('returns null for null', () => {
      expect(formatDuration(null as unknown as undefined)).toBeNull();
    });

    it('returns null for invalid string', () => {
      expect(formatDuration('invalid')).toBeNull();
    });

    it('returns null for zero seconds', () => {
      expect(formatDuration(0)).toBeNull();
    });

    it('returns null for negative seconds', () => {
      expect(formatDuration(-10)).toBeNull();
    });

    it('returns null for empty string', () => {
      expect(formatDuration('')).toBeNull();
    });

    it('handles decimal seconds in ISO format', () => {
      expect(formatDuration('PT3M30.5S')).toBe('3:30');
    });

    it('returns null for Infinity', () => {
      expect(formatDuration(Infinity)).toBeNull();
    });

    it('returns null for non-object types', () => {
      expect(formatDuration({} as unknown as number)).toBeNull();
    });
  });
});

describe('durationToSeconds - additional edge cases', () => {
  it('returns null for Infinity', () => {
    expect(durationToSeconds(Infinity)).toBeNull();
  });

  it('returns null for non-object types', () => {
    expect(durationToSeconds({} as unknown as number)).toBeNull();
  });
});

describe('durationToSeconds', () => {
  describe('TimeSpan format (HH:MM:SS)', () => {
    it('converts 3 minutes 30 seconds to 210', () => {
      expect(durationToSeconds('00:03:30')).toBe(210);
    });

    it('converts 45 seconds', () => {
      expect(durationToSeconds('00:00:45')).toBe(45);
    });

    it('converts 1 hour 23 minutes 45 seconds', () => {
      expect(durationToSeconds('01:23:45')).toBe(5025);
    });
  });

  describe('ISO 8601 duration format', () => {
    it('converts PT3M30S to 210', () => {
      expect(durationToSeconds('PT3M30S')).toBe(210);
    });

    it('converts PT45S to 45', () => {
      expect(durationToSeconds('PT45S')).toBe(45);
    });

    it('converts PT1H23M45S to 5025', () => {
      expect(durationToSeconds('PT1H23M45S')).toBe(5025);
    });

    it('returns null for malformed ISO 8601', () => {
      expect(durationToSeconds('PT')).toBeNull();
      expect(durationToSeconds('PTXYZ')).toBeNull();
    });
  });

  describe('Numeric input', () => {
    it('returns numeric input as-is', () => {
      expect(durationToSeconds(210)).toBe(210);
    });

    it('floors decimal input', () => {
      expect(durationToSeconds(210.7)).toBe(210);
    });
  });

  describe('Numeric string input', () => {
    it('parses numeric string "210" to 210', () => {
      expect(durationToSeconds('210')).toBe(210);
    });

    it('parses decimal string "210.5" and floors it', () => {
      expect(durationToSeconds('210.5')).toBe(210);
    });
  });

  describe('Edge cases', () => {
    it('returns null for undefined', () => {
      expect(durationToSeconds(undefined)).toBeNull();
    });

    it('returns null for null', () => {
      expect(durationToSeconds(null as unknown as undefined)).toBeNull();
    });

    it('returns null for invalid string', () => {
      expect(durationToSeconds('invalid')).toBeNull();
    });

    it('returns null for zero seconds', () => {
      expect(durationToSeconds(0)).toBeNull();
    });

    it('returns null for negative seconds', () => {
      expect(durationToSeconds(-10)).toBeNull();
    });

    it('handles decimal seconds in ISO format', () => {
      expect(durationToSeconds('PT3M30.5S')).toBe(210);
    });
  });
});
