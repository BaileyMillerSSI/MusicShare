import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { BreadstickFooter } from './BreadstickFooter';

describe('BreadstickFooter', () => {
  it('links to Breadstick Labs with referral tracking parameters', () => {
    render(<BreadstickFooter />);

    expect(screen.getByRole('link', { name: /proudly baked by breadstick labs/i })).toHaveAttribute(
      'href',
      'https://breadsticklabs.com/?utm_source=musicshare&utm_medium=referral&utm_campaign=musicshare_footer'
    );
  });

  it('opens the external link safely in a new tab', () => {
    render(<BreadstickFooter />);

    expect(screen.getByRole('link')).toHaveAttribute('target', '_blank');
    expect(screen.getByRole('link')).toHaveAttribute('rel', 'noreferrer');
  });
});
