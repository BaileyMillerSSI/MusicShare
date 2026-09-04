import { describe, expect, it } from 'vitest';
import { expectedFields, validateSecurityTxt } from './assert-security-txt.mjs';

const validRecord = `${[...expectedFields]
  .map(([name, value]) => `${name}: ${value}`)
  .join('\n')}\n`;
const now = new Date('2026-09-03T00:00:00Z');

describe('validateSecurityTxt', () => {
  it('accepts the required security.txt record', () => {
    expect(() => validateSecurityTxt(validRecord, now)).not.toThrow();
  });

  it('rejects a missing record or changed content', () => {
    expect(() => validateSecurityTxt('', now)).toThrow('must end with a newline');
    expect(() => validateSecurityTxt(validRecord.replace('Preferred-Languages: en', 'Preferred-Languages: fr'), now)).toThrow(
      'Preferred-Languages must be en',
    );
  });

  it('rejects invalid or insufficient expiry', () => {
    expect(() => validateSecurityTxt(validRecord.replace('2027-09-03T00:00:00Z', 'not-a-date'), now)).toThrow(
      'Expires must be 2027-09-03T00:00:00Z',
    );
    expect(() => validateSecurityTxt(validRecord, new Date('2027-08-05T00:00:01Z'))).toThrow(
      'at least 30 days in the future',
    );
  });

  it('rejects unexpected and duplicate fields', () => {
    expect(() => validateSecurityTxt(validRecord.replace('Preferred-Languages: en\n', 'Policy: https://example.com\n'), now)).toThrow(
      'required names and order',
    );
    expect(() => validateSecurityTxt(`${validRecord}Preferred-Languages: en\n`, now)).toThrow(
      'exactly four non-empty fields',
    );
  });
});
