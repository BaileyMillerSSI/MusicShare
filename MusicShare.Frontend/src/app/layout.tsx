import './globals.css';
import { QueryClientWrapper } from '../components/QueryClientWrapper';

export const metadata = {
  title: 'MusicShare',
  description: 'Share music across platforms',
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
