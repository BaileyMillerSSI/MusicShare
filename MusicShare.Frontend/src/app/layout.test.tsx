import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

// Mock QueryClientWrapper
vi.mock('../components/QueryClientWrapper', () => ({
  QueryClientWrapper: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="query-client-wrapper">{children}</div>
  ),
}));

// Mock PWAInstallPrompt
vi.mock('../components/PWAInstallPrompt', () => ({
  PWAInstallPrompt: () => <div data-testid="pwa-install-prompt">PWA Install Prompt</div>,
}));

import RootLayout from './layout';

describe('RootLayout', () => {
  describe('Component Structure', () => {
    it('renders QueryClientWrapper as the top-level wrapper', () => {
      render(
        <RootLayout>
          <div>Test content</div>
        </RootLayout>
      );

      const wrapper = screen.getByTestId('query-client-wrapper');
      expect(wrapper).toBeInTheDocument();
    });

    it('contains all required child components', () => {
      render(
        <RootLayout>
          <div data-testid="test-content">Test content</div>
        </RootLayout>
      );

      expect(screen.getByTestId('query-client-wrapper')).toBeInTheDocument();
      expect(screen.getByTestId('test-content')).toBeInTheDocument();
      expect(screen.getByTestId('pwa-install-prompt')).toBeInTheDocument();
    });
  });

  describe('Children Rendering', () => {
    it('renders children correctly', () => {
      render(
        <RootLayout>
          <div data-testid="child-content">Test child content</div>
        </RootLayout>
      );

      expect(screen.getByTestId('child-content')).toBeInTheDocument();
      expect(screen.getByText('Test child content')).toBeInTheDocument();
    });

    it('renders multiple children', () => {
      render(
        <RootLayout>
          <div data-testid="child-1">First child</div>
          <div data-testid="child-2">Second child</div>
          <div data-testid="child-3">Third child</div>
        </RootLayout>
      );

      expect(screen.getByTestId('child-1')).toBeInTheDocument();
      expect(screen.getByTestId('child-2')).toBeInTheDocument();
      expect(screen.getByTestId('child-3')).toBeInTheDocument();
    });

    it('renders complex nested children', () => {
      render(
        <RootLayout>
          <main>
            <header>
              <h1>Title</h1>
            </header>
            <section>
              <p>Content</p>
            </section>
          </main>
        </RootLayout>
      );

      expect(screen.getByRole('main')).toBeInTheDocument();
      expect(screen.getByRole('banner')).toBeInTheDocument();
      expect(screen.getByText('Title')).toBeInTheDocument();
      expect(screen.getByText('Content')).toBeInTheDocument();
    });

    it('handles empty children', () => {
      const { container } = render(<RootLayout>{null}</RootLayout>);

      const wrapper = screen.getByTestId('query-client-wrapper');
      expect(wrapper).toBeInTheDocument();
      // Wrapper should be present even with no children
    });
  });

  describe('QueryClientWrapper Integration', () => {
    it('wraps children in QueryClientWrapper', () => {
      render(
        <RootLayout>
          <div data-testid="wrapped-content">Wrapped</div>
        </RootLayout>
      );

      const wrapper = screen.getByTestId('query-client-wrapper');
      const content = screen.getByTestId('wrapped-content');

      expect(wrapper).toBeInTheDocument();
      expect(wrapper).toContainElement(content);
    });

    it('renders QueryClientWrapper as top-level component', () => {
      const { container } = render(
        <RootLayout>
          <div>Content</div>
        </RootLayout>
      );

      const wrapper = screen.getByTestId('query-client-wrapper');

      // Wrapper should be in the container
      expect(container).toContainElement(wrapper);
    });
  });

  describe('PWAInstallPrompt Integration', () => {
    it('includes PWAInstallPrompt component', () => {
      render(
        <RootLayout>
          <div>Content</div>
        </RootLayout>
      );

      expect(screen.getByTestId('pwa-install-prompt')).toBeInTheDocument();
    });

    it('renders PWAInstallPrompt as sibling to children', () => {
      render(
        <RootLayout>
          <div data-testid="main-content">Main content</div>
        </RootLayout>
      );

      const mainContent = screen.getByTestId('main-content');
      const pwaPrompt = screen.getByTestId('pwa-install-prompt');
      const wrapper = screen.getByTestId('query-client-wrapper');

      // Both should be inside the QueryClientWrapper
      expect(wrapper).toContainElement(mainContent);
      expect(wrapper).toContainElement(pwaPrompt);
    });

    it('renders PWAInstallPrompt after children in DOM order', () => {
      const { container } = render(
        <RootLayout>
          <div data-testid="child">Child</div>
        </RootLayout>
      );

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
      const { container } = render(
        <RootLayout>
          <div data-testid="page-content">Page content</div>
        </RootLayout>
      );

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
      render(
        <RootLayout>
          <div className="min-h-screen">
            <main className="container">
              <h1>MusicShare</h1>
              <p>Share music across platforms</p>
            </main>
          </div>
        </RootLayout>
      );

      expect(screen.getByText('MusicShare')).toBeInTheDocument();
      expect(screen.getByText('Share music across platforms')).toBeInTheDocument();
      expect(screen.getByTestId('query-client-wrapper')).toBeInTheDocument();
      expect(screen.getByTestId('pwa-install-prompt')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('handles children with fragments', () => {
      render(
        <RootLayout>
          <>
            <div data-testid="fragment-child-1">First</div>
            <div data-testid="fragment-child-2">Second</div>
          </>
        </RootLayout>
      );

      expect(screen.getByTestId('fragment-child-1')).toBeInTheDocument();
      expect(screen.getByTestId('fragment-child-2')).toBeInTheDocument();
    });

    it('handles children with conditional rendering', () => {
      const showContent = true;

      render(
        <RootLayout>
          {showContent && <div data-testid="conditional">Conditional content</div>}
        </RootLayout>
      );

      expect(screen.getByTestId('conditional')).toBeInTheDocument();
    });

    it('handles children with mapped elements', () => {
      const items = ['Item 1', 'Item 2', 'Item 3'];

      render(
        <RootLayout>
          <ul>
            {items.map((item, index) => (
              <li key={index}>{item}</li>
            ))}
          </ul>
        </RootLayout>
      );

      expect(screen.getByText('Item 1')).toBeInTheDocument();
      expect(screen.getByText('Item 2')).toBeInTheDocument();
      expect(screen.getByText('Item 3')).toBeInTheDocument();
    });

    it('handles deeply nested children', () => {
      render(
        <RootLayout>
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
        </RootLayout>
      );

      expect(screen.getByTestId('deeply-nested')).toBeInTheDocument();
    });

    it('renders correctly with different types of content', () => {
      render(
        <RootLayout>
          <div>
            <p>Text content</p>
            <button>Button</button>
            <input type="text" placeholder="Input" />
            <img src="/test.jpg" alt="Image" />
            <a href="/link">Link</a>
          </div>
        </RootLayout>
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

      render(<RootLayout {...readonlyChildren} />);

      expect(screen.getByTestId('readonly-child')).toBeInTheDocument();
    });
  });
});
