import type { NextConfig } from 'next';

const apiTarget =
  process.env.services__api__https__0 ??
  process.env.services__api__http__0 ??
  'http://localhost:5222';

const nextConfig: NextConfig = {
  rewrites: async () => [
    {
      source: '/api/:path*',
      destination: `${apiTarget}/api/:path*`,
    },
  ],
};

export default nextConfig;
