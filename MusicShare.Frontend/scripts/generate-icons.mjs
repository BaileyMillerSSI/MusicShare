import sharp from 'sharp';
import { mkdir } from 'fs/promises';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const publicDir = join(__dirname, '..', 'public');
const iconsDir = join(publicDir, 'icons');

const sizes = [192, 512];

async function generateIcons() {
  await mkdir(iconsDir, { recursive: true });

  const svgPath = join(publicDir, 'favicon.svg');

  for (const size of sizes) {
    // Standard icon
    await sharp(svgPath)
      .resize(size, size)
      .png()
      .toFile(join(iconsDir, `icon-${size}x${size}.png`));

    console.log(`Generated icon-${size}x${size}.png`);

    // Maskable version (same image - favicon has good padding for maskable)
    await sharp(svgPath)
      .resize(size, size)
      .png()
      .toFile(join(iconsDir, `icon-${size}x${size}-maskable.png`));

    console.log(`Generated icon-${size}x${size}-maskable.png`);
  }

  // Apple touch icon (180x180)
  await sharp(svgPath)
    .resize(180, 180)
    .png()
    .toFile(join(publicDir, 'apple-touch-icon.png'));

  console.log('Generated apple-touch-icon.png');

  console.log('All icons generated successfully!');
}

generateIcons().catch(console.error);
