import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

// Mock QueryClientWrapper
vi.mock('../components/QueryClientWrapper', () => ({
  QueryClientWrapper: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="query-client-wrapper">{children}</div>
  ),
}));

// Mock PWAInstallPrompt with the correct path
vi.mock('../components/PWAInstallPrompt/PWAInstallPrompt', () => ({
  PWAInstallPrompt: () => <div data-testid="pwa-install-prompt">PWA Install Prompt</div>,
}));

import RootLayout from './layout';

// Helper function to render just the body content of the layout
function renderLayoutBody(children: React.ReactNode) {
  const result = render(<RootLayout>{children}</RootLayout>);
  // The layout renders html > body > QueryClientWrapper
  // In test environment, we access the body content directly
  return result;
}

describe('RootLayout', () => {
  describe('Component Structure', () => {
    it('renders QueryClientWrapper as the top-level wrapper', () => {
      renderLayoutBody(<div>Test content</div>);

      const wrapper = screen.getByTestId('query-client-wrapper');
      expect(wrapper).toBeInTheDocument();
    });

    it('contains all required child components', () => {
      renderLayoutBody(<div data-testid="test-content">Test content</div>);

      expect(screen.getByTestId('query-client-wrapper')).toBeInTheDocument();
      expect(screen.getByTestId('test-content')).toBeInTheDocument();
      expect(screen.getByTestId('pwa-install-prompt')).toBeInTheDocument();
    });
  });

  describe('Children Rendering', () => {
    it('renders children correctly', () => {
      renderLayoutBody(<div data-testid="child-content">Test child content</div>);

      expect(screen.getByTestId('child-content')).toBeInTheDocument();
      expect(screen.getByText('Test child content')).toBeInTheDocument();
    });

    it('renders multiple children', () => {
      renderLayoutBody(
        <>
          <div data-testid="child-1">First child</div>
          <div data-testid="child-2">Second child</div>
          <div data-testid="child-3">Third child</div>
        </>
      );

      expect(screen.getByTestId('child-1')).toBeInTheDocument();
      expect(screen.getByTestId('child-2')).toBeInTheDocument();
      expect(screen.getByTestId('child-3')).toBeInTheDocument();
    });

    it('renders complex nested children', () => {
      renderLayoutBody(
        <main>
          <header>
            <h1>Title</h1>
          </header>
          <section>
            <p>Content</p>
          </section>
        </main>
      );

      expect(screen.getByRole('main')).toBeInTheDocument();
      expect(screen.getByRole('banner')).toBeInTheDocument();
      expect(screen.getByText('Title')).toBeInTheDocument();
      expect(screen.getByText('Content')).toBeInTheDocument();
    });

    it('handles empty children', () => {
      renderLayoutBody(null);

      const wrapper = screen.getByTestId('query-client-wrapper');
      expect(wrapper).toBeInTheDocument();
      // Wrapper should be present even with no children
    });
  });

  describe('QueryClientWrapper Integration', () => {
    it('wraps children in QueryClientWrapper', () => {
      renderLayoutBody(<div data-testid="wrapped-content">Wrapped</div>);

      const wrapper = screen.getByTestId('query-client-wrapper');
      const content = screen.getByTestId('wrapped-content');

      expect(wrapper).toBeInTheDocument();
      expect(wrapper).toContainElement(content);
    });

    it('renders QueryClientWrapper as top-level component', () => {
      const { container } = renderLayoutBody(<div>Content</div>);

      const wrapper = screen.getByTestId('query-client-wrapper');

      // Wrapper should be in the container
      expect(container).toContainElement(wrapper);
    });
  });

  describe('PWAInstallPrompt Integration', () => {
    it('includes PWAInstallPrompt component', () => {
      renderLayoutBody(<div>Content</div>);

      expect(screen.getByTestId('pwa-install-prompt')).toBeInTheDocument();
    });

    it('renders PWAInstallPrompt as sibling to children', () => {
      renderLayoutBody(<div data-testid="main-content">Main content</div>);

      const mainContent = screen.getByTestId('main-content');
      const pwaPrompt = screen.getByTestId('pwa-install-prompt');
      const wrapper = screen.getByTestId('query-client-wrapper');

      // Both should be inside the QueryClientWrapper
      expect(wrapper).toContainElement(mainContent);
      expect(wrapper).toContainElement(pwaPrompt);
    });

    it('renders PWAInstallPrompt after children in DOM order', () => {
      renderLayoutBody(<div data-testid="child">Child</div>);

      const wrapper = screen.getByTestId('query-client-wrapper');
      const children = Array.from(wrapper.children);

      // PWAInstallPrompt should be after the children
      const childIndex = children.findIndex((el) => el.getAttribute('data-testid') === 'child');
      const promptIndex = children.findIndex(
        (el) => el.getAttribute('data-testid') === 'pwa-install-prompt'
      );

      expect(promptIndex).toBeGreaterThan(childIndex);
    });
  });

  describe('Layout Composition', () => {
    it('renders all components in correct order', () => {
      const { container } = renderLayoutBody(<div data-testid="page-content">Page content</div>);

      // Expected structure:
      // QueryClientWrapper > [children, PWAInstallPrompt]
      const wrapper = screen.getByTestId('query-client-wrapper');
      const content = screen.getByTestId('page-content');
      const prompt = screen.getByTestId('pwa-install-prompt');

      expect(container).toContainElement(wrapper);
      expect(wrapper).toContainElement(content);
      expect(wrapper).toContainElement(prompt);
    });

    it('maintains proper nesting with real page structure', () => {
      renderLayoutBody(
        <div className="min-h-screen">
          <main className="container">
            <h1>MusicShare</h1>
            <p>Share music across platforms</p>
          </main>
        </div>
      );

      expect(screen.getByText('MusicShare')).toBeInTheDocument();
      expect(screen.getByText('Share music across platforms')).toBeInTheDocument();
      expect(screen.getByTestId('query-client-wrapper')).toBeInTheDocument();
      expect(screen.getByTestId('pwa-install-prompt')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('handles children with fragments', () => {
      renderLayoutBody(
        <>
          <div data-testid="fragment-child-1">First</div>
          <div data-testid="fragment-child-2">Second</div>
        </>
      );

      expect(screen.getByTestId('fragment-child-1')).toBeInTheDocument();
      expect(screen.getByTestId('fragment-child-2')).toBeInTheDocument();
    });

    it('handles children with conditional rendering', () => {
      const showContent = true;

      renderLayoutBody(
        showContent ? <div data-testid="conditional">Conditional content</div> : null
      );

      expect(screen.getByTestId('conditional')).toBeInTheDocument();
    });

    it('handles children with mapped elements', () => {
      const items = ['Item 1', 'Item 2', 'Item 3'];

      renderLayoutBody(
        <ul>
          {items.map((item, index) => (
            <li key={index}>{item}</li>
          ))}
        </ul>
      );

      expect(screen.getByText('Item 1')).toBeInTheDocument();
      expect(screen.getByText('Item 2')).toBeInTheDocument();
      expect(screen.getByText('Item 3')).toBeInTheDocument();
    });

    it('handles deeply nested children', () => {
      renderLayoutBody(
        <div>
          <div>
            <div>
              <div>
                <div>
                  <span data-testid="deeply-nested">Deeply nested content</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      );

      expect(screen.getByTestId('deeply-nested')).toBeInTheDocument();
    });

    it('renders correctly with different types of content', () => {
      renderLayoutBody(
        <div>
          <p>Text content</p>
          <button>Button</button>
          <input type="text" placeholder="Input" />
          <img src="/test.jpg" alt="Image" />
          <a href="/link">Link</a>
        </div>
      );

      expect(screen.getByText('Text content')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Button' })).toBeInTheDocument();
      expect(screen.getByPlaceholderText('Input')).toBeInTheDocument();
      expect(screen.getByAltText('Image')).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Link' })).toBeInTheDocument();
    });
  });

  describe('Readonly Props', () => {
    it('accepts readonly children prop', () => {
      const readonlyChildren: Readonly<{ children: React.ReactNode }> = {
        children: <div data-testid="readonly-child">Readonly child</div>,
      };

      renderLayoutBody(readonlyChildren.children);

      expect(screen.getByTestId('readonly-child')).toBeInTheDocument();
    });
  });
});
