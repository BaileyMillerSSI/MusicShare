import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { BreadstickFooter } from './BreadstickFooter';

describe('BreadstickFooter', () => {
  it('renders the requested attribution and exact Breadstick Labs destination', () => {
    render(<BreadstickFooter />);

    expect(screen.getByText('Proudly baked by', { exact: false })).toHaveTextContent(
      'Proudly baked by Breadstick Labs'
    );
    expect(screen.getByRole('link', { name: 'Breadstick Labs' })).toHaveAttribute(
      'href',
      'https://breadsticklabs.com/'
    );
  });
});
