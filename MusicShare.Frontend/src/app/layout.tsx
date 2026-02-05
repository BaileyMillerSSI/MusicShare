import './globals.css';
import { QueryClientWrapper } from '../components/QueryClientWrapper';
import type { Metadata, Viewport } from 'next';

export const metadata: Metadata = {
  title: 'MusicShare',
  description: 'Share music across platforms',
  applicationName: 'MusicShare',
  manifest: '/manifest.json',
  icons: {
    icon: '/favicon.svg',
    apple: '/icons/ios/180.png',
  },
  appleWebApp: {
    capable: true,
    statusBarStyle: 'default',
    title: 'MusicShare',
  },
  formatDetection: {
    telephone: false,
  },
};

export const viewport: Viewport = {
  themeColor: '#a855f7',
  width: 'device-width',
  initialScale: 1,
  maximumScale: 1,
};

export default function RootLayout({
  children,
}: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="en">
      <body>
        <QueryClientWrapper>{children}</QueryClientWrapper>
      </body>
    </html>
  );
}
