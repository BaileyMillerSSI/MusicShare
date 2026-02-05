import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import InstallBanner from './InstallBanner';

describe('InstallBanner', () => {
  it('renders the banner heading and description', () => {
    render(<InstallBanner onInstall={vi.fn()} onDismiss={vi.fn()} />);

    expect(
      screen.getByText('Install Music Share')
    ).toBeVisible();
    expect(
      screen.getByText('Add to home screen for quick access')
    ).toBeVisible();
  });

  it('renders the Install and Not now buttons', () => {
    render(<InstallBanner onInstall={vi.fn()} onDismiss={vi.fn()} />);

    expect(
      screen.getByRole('button', { name: 'Install' })
    ).toBeVisible();
    expect(
      screen.getByRole('button', { name: 'Not now' })
    ).toBeVisible();
  });

  it('calls onInstall when the Install button is clicked', async () => {
    const onInstall = vi.fn();
    const user = userEvent.setup();

    render(<InstallBanner onInstall={onInstall} onDismiss={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: 'Install' }));

    expect(onInstall).toHaveBeenCalledTimes(1);
  });

  it('calls onDismiss when the Not now button is clicked', async () => {
    const onDismiss = vi.fn();
    const user = userEvent.setup();

    render(<InstallBanner onInstall={vi.fn()} onDismiss={onDismiss} />);
    await user.click(screen.getByRole('button', { name: 'Not now' }));

    expect(onDismiss).toHaveBeenCalledTimes(1);
  });

  it('does not call onDismiss when Install is clicked', async () => {
    const onDismiss = vi.fn();
    const user = userEvent.setup();

    render(<InstallBanner onInstall={vi.fn()} onDismiss={onDismiss} />);
    await user.click(screen.getByRole('button', { name: 'Install' }));

    expect(onDismiss).not.toHaveBeenCalled();
  });

  it('does not call onInstall when Not now is clicked', async () => {
    const onInstall = vi.fn();
    const user = userEvent.setup();

    render(<InstallBanner onInstall={onInstall} onDismiss={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: 'Not now' }));

    expect(onInstall).not.toHaveBeenCalled();
  });
});
