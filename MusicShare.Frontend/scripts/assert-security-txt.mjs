import { readFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';

export const expectedFields = new Map([
  ['Contact', 'https://breadsticklabs.com/#contact'],
  ['Expires', '2027-09-03T00:00:00Z'],
  ['Canonical', 'https://music.baileymiller.dev/.well-known/security.txt'],
  ['Preferred-Languages', 'en'],
]);

const minimumValidityMilliseconds = 30 * 24 * 60 * 60 * 1000;
const utcTimestampPattern = /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$/;

export function parseSecurityTxt(content) {
  if (!content.endsWith('\n')) {
    throw new Error('security.txt must end with a newline.');
  }

  const lines = content.slice(0, -1).split('\n');
  if (lines.length !== expectedFields.size || lines.some((line) => line.length === 0)) {
    throw new Error('security.txt must contain exactly four non-empty fields.');
  }

  const fields = new Map();
  for (const line of lines) {
    const separatorIndex = line.indexOf(': ');
    if (separatorIndex <= 0 || line.indexOf(': ', separatorIndex + 2) !== -1) {
      throw new Error(`security.txt has an invalid field: ${line}`);
    }

    const name = line.slice(0, separatorIndex);
    const value = line.slice(separatorIndex + 2);
    if (!value || fields.has(name)) {
      throw new Error(`security.txt has a duplicate or empty field: ${name}`);
    }

    fields.set(name, value);
  }

  return fields;
}

export function validateSecurityTxt(content, now = new Date()) {
  const fields = parseSecurityTxt(content);
  const expectedNames = [...expectedFields.keys()];
  const actualNames = [...fields.keys()];

  if (!actualNames.every((name, index) => name === expectedNames[index])) {
    throw new Error('security.txt fields must use the required names and order.');
  }

  for (const [name, expectedValue] of expectedFields) {
    if (fields.get(name) !== expectedValue) {
      throw new Error(`security.txt ${name} must be ${expectedValue}.`);
    }
  }

  const expires = fields.get('Expires');
  if (!utcTimestampPattern.test(expires)) {
    throw new Error('security.txt Expires must be a UTC timestamp.');
  }

  const expiration = new Date(expires);
  if (
    Number.isNaN(expiration.getTime()) ||
    expiration.toISOString().replace('.000Z', 'Z') !== expires
  ) {
    throw new Error('security.txt Expires must be a valid UTC timestamp.');
  }

  if (expiration.getTime() - now.getTime() < minimumValidityMilliseconds) {
    throw new Error('security.txt Expires must be at least 30 days in the future.');
  }
}

async function main() {
  const securityTxtUrl = new URL('../public/.well-known/security.txt', import.meta.url);
  const content = await readFile(securityTxtUrl, 'utf8');
  validateSecurityTxt(content);
}

if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
