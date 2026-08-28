import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import Home from './page';

// Mock ShareForm component
vi.mock('../components/ShareForm', () => ({
  ShareForm: () => <div data-testid="share-form">Share Form</div>,
}));

describe('Home page', () => {
  it('renders the page title "Music Share"', () => {
    render(<Home />);
    expect(screen.getByRole('heading', { name: /music share/i })).toBeInTheDocument();
  });

  it('renders the subtitle', () => {
    render(<Home />);
    expect(screen.getByText(/share music across platforms/i)).toBeInTheDocument();
  });

  it('shows the "How it works" instructions', () => {
    render(<Home />);
    expect(screen.getByText(/how it works:/i)).toBeInTheDocument();
  });

  it('shows all instruction steps', () => {
    render(<Home />);
    expect(screen.getByText(/paste a song url from any supported service/i)).toBeInTheDocument();
    expect(screen.getByText(/we'll find it on other platforms/i)).toBeInTheDocument();
    expect(screen.getByText(/share the universal link with anyone/i)).toBeInTheDocument();
  });

  it('contains the ShareForm component', () => {
    render(<Home />);
    expect(screen.getByTestId('share-form')).toBeInTheDocument();
  });

  it('renders the Breadstick Labs footer beneath the primary card', () => {
    const { container } = render(<Home />);

    const card = container.querySelector('.bg-white.rounded-lg.shadow-xl');
    const footer = screen.getByRole('contentinfo');

    expect(card?.compareDocumentPosition(footer)).toBe(Node.DOCUMENT_POSITION_FOLLOWING);
  });

  it('has the correct Suspense structure', () => {
    const { container } = render(<Home />);
    // Verify there's a Suspense boundary by checking the structure
    // Since ShareForm is inside Suspense, it should render without the fallback in tests
    expect(screen.getByTestId('share-form')).toBeInTheDocument();

    // Verify the fallback element doesn't exist (it would only show while loading)
    const fallbackElement = container.querySelector('.animate-pulse.h-48.bg-gray-100.rounded-lg');
    expect(fallbackElement).not.toBeInTheDocument();
  });

  it('has proper styling classes', () => {
    const { container } = render(<Home />);
    const mainContainer = container.querySelector('.min-h-screen.bg-linear-to-br');
    expect(mainContainer).toBeInTheDocument();
    expect(mainContainer).toHaveClass('flex-col');
    expect(mainContainer).toHaveClass('gap-4');

    const card = container.querySelector('.bg-white.rounded-lg.shadow-xl');
    expect(card).toBeInTheDocument();
  });
});
