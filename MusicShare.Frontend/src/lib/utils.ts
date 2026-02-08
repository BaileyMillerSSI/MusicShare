// Utility functions for formatting and data transformation

/**
 * Formats a duration string to human-readable format.
 *
 * Handles multiple input formats:
 * - TimeSpan string format: "00:03:30" or "3:30:00"
 * - ISO 8601 duration: "PT3M30S"
 * - Raw number (treated as seconds)
 *
 * @param duration - Duration as string (TimeSpan/ISO8601) or number (seconds), or undefined
 * @returns Formatted duration like "3m30s" or "59s", or null if invalid/unavailable
 *
 * @example
 * formatDuration("00:03:30") // "3m30s"
 * formatDuration("PT3M30S") // "3m30s"
 * formatDuration(210) // "3m30s"
 * formatDuration("00:00:45") // "45s"
 * formatDuration(undefined) // null
 */
export function formatDuration(duration: string | number | undefined): string | null {
  if (duration === undefined || duration === null) {
    return null;
  }

  let totalSeconds: number | undefined;

  if (typeof duration === 'number') {
    totalSeconds = duration;
  } else if (typeof duration === 'string') {
    // Try parsing as TimeSpan format (HH:MM:SS or H:MM:SS, with optional fractional seconds)
    const timeSpanMatch = duration.match(/^(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d+))?$/);
    if (timeSpanMatch) {
      const hours = parseInt(timeSpanMatch[1], 10);
      const minutes = parseInt(timeSpanMatch[2], 10);
      const seconds = parseFloat(timeSpanMatch[3]);
      const fractional = timeSpanMatch[4];
      const fractionalSeconds = fractional ? parseFloat('0.' + fractional) : 0;
      totalSeconds = hours * 3600 + minutes * 60 + seconds + fractionalSeconds;
    }
    // Try parsing as MM:SS format with optional fractional seconds
    else {
      const mmssMatch = duration.match(/^(\d{1,2}):(\d{2})(?:\.(\d+))?$/);
      if (mmssMatch) {
        const minutes = parseInt(mmssMatch[1], 10);
        const seconds = parseFloat(mmssMatch[2]);
        const fractional = mmssMatch[3];
        const fractionalSeconds = fractional ? parseFloat('0.' + fractional) : 0;
        totalSeconds = minutes * 60 + seconds + fractionalSeconds;
      }
      // Try parsing as ISO 8601 duration (PT3M30S)
      else if (duration.startsWith('PT')) {
        const iso8601Match = duration.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?/);
        if (iso8601Match) {
          const hours = parseInt(iso8601Match[1] || '0', 10);
          const minutes = parseInt(iso8601Match[2] || '0', 10);
          const seconds = parseFloat(iso8601Match[3] || '0');
          totalSeconds = hours * 3600 + minutes * 60 + seconds;
        } else {
          return null;
        }
      } else {
        // Try parsing as plain number string
        const parsed = parseFloat(duration);
        if (isNaN(parsed)) {
          return null;
        }
        totalSeconds = parsed;
      }
    }
  } else {
    return null;
  }

  if (totalSeconds === undefined || totalSeconds <= 0 || !isFinite(totalSeconds)) {
    return null;
  }

  if (totalSeconds <= 0 || !isFinite(totalSeconds)) {
    return null;
  }

  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = Math.floor(totalSeconds % 60);

  if (hours > 0) {
    return `${hours}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
  } else {
    return `${minutes}:${seconds.toString().padStart(2, '0')}`;
  }
}

/**
 * Converts duration to total seconds for meta tags.
 *
 * @param duration - Duration as string (TimeSpan/ISO8601) or number (seconds), or undefined
 * @returns Total seconds as number, or null if invalid/unavailable
 */
export function durationToSeconds(duration: string | number | undefined): number | null {
  if (duration === undefined || duration === null) {
    return null;
  }

  let totalSeconds: number | undefined;

  if (typeof duration === 'number') {
    totalSeconds = duration;
  } else if (typeof duration === 'string') {
    // Try parsing as TimeSpan format (HH:MM:SS, with optional fractional seconds)
    const timeSpanMatch = duration.match(/^(\d{1,2}):(\d{2}):(\d{2})(?:\.(\d+))?$/);
    if (timeSpanMatch) {
      const hours = parseInt(timeSpanMatch[1], 10);
      const minutes = parseInt(timeSpanMatch[2], 10);
      const seconds = parseFloat(timeSpanMatch[3]);
      const fractional = timeSpanMatch[4];
      const fractionalSeconds = fractional ? parseFloat('0.' + fractional) : 0;
      totalSeconds = hours * 3600 + minutes * 60 + seconds + fractionalSeconds;
    }
    // Try parsing as MM:SS format with optional fractional seconds
    else {
      const mmssMatch = duration.match(/^(\d{1,2}):(\d{2})(?:\.(\d+))?$/);
      if (mmssMatch) {
        const minutes = parseInt(mmssMatch[1], 10);
        const seconds = parseFloat(mmssMatch[2]);
        const fractional = mmssMatch[3];
        const fractionalSeconds = fractional ? parseFloat('0.' + fractional) : 0;
        totalSeconds = minutes * 60 + seconds + fractionalSeconds;
      }
      // Try parsing as ISO 8601 duration (PT3M30S)
      else if (duration.startsWith('PT')) {
        const iso8601Match = duration.match(/PT(?:(\d+)H)?(?:(\d+)M)?(?:(\d+(?:\.\d+)?)S)?/);
        if (iso8601Match) {
          const hours = parseInt(iso8601Match[1] || '0', 10);
          const minutes = parseInt(iso8601Match[2] || '0', 10);
          const seconds = parseFloat(iso8601Match[3] || '0');
          totalSeconds = hours * 3600 + minutes * 60 + seconds;
        } else {
          return null;
        }
      } else {
        // Try parsing as plain number string
        const parsed = parseFloat(duration);
        if (isNaN(parsed)) {
          return null;
        }
        totalSeconds = parsed;
      }
    }
  } else {
    return null;
  }

  if (totalSeconds === undefined || totalSeconds <= 0 || !isFinite(totalSeconds)) {
    return null;
  }

  return Math.floor(totalSeconds);
}
