import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import IOSInstallInstructions from './IOSInstallInstructions';

describe('IOSInstallInstructions', () => {
  it('renders the heading', () => {
    render(<IOSInstallInstructions onDismiss={vi.fn()} />);

    expect(
      screen.getByRole('heading', { name: 'Install Music Share' })
    ).toBeVisible();
  });

  it('renders the introductory instruction text', () => {
    render(<IOSInstallInstructions onDismiss={vi.fn()} />);

    expect(
      screen.getByText(
        'Add Music Share to your home screen for the best experience:'
      )
    ).toBeVisible();
  });

  it('renders all three numbered steps in an ordered list', () => {
    render(<IOSInstallInstructions onDismiss={vi.fn()} />);

    const list = screen.getByRole('list');
    const items = screen.getAllByRole('listitem');

    expect(list.tagName).toBe('OL');
    expect(items).toHaveLength(3);
  });

  it('step 1 instructs the user to tap the Share button in Safari', () => {
    render(<IOSInstallInstructions onDismiss={vi.fn()} />);

    const items = screen.getAllByRole('listitem');
    expect(items[0]).toHaveTextContent(/Share.*button in Safari/);
  });

  it('step 2 instructs the user to tap Add to Home Screen', () => {
    render(<IOSInstallInstructions onDismiss={vi.fn()} />);

    const items = screen.getAllByRole('listitem');
    expect(items[1]).toHaveTextContent(/Add to Home Screen/);
  });

  it('step 3 instructs the user to tap Add to confirm', () => {
    render(<IOSInstallInstructions onDismiss={vi.fn()} />);

    const items = screen.getAllByRole('listitem');
    expect(items[2]).toHaveTextContent(/Tap.*Add.*to confirm/);
  });

  it('renders the Got it dismiss button', () => {
    render(<IOSInstallInstructions onDismiss={vi.fn()} />);

    expect(
      screen.getByRole('button', { name: 'Got it' })
    ).toBeVisible();
  });

  it('calls onDismiss when the Got it button is clicked', async () => {
    const onDismiss = vi.fn();
    const user = userEvent.setup();

    render(<IOSInstallInstructions onDismiss={onDismiss} />);
    await user.click(screen.getByRole('button', { name: 'Got it' }));

    expect(onDismiss).toHaveBeenCalledTimes(1);
  });

  it('does not call onDismiss before the button is clicked', () => {
    const onDismiss = vi.fn();

    render(<IOSInstallInstructions onDismiss={onDismiss} />);

    expect(onDismiss).not.toHaveBeenCalled();
  });
});
