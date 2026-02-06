import { render, screen, waitFor, act } from '@testing-library/react';
import { useQuery } from '@tanstack/react-query';
import { QueryClientWrapper } from './QueryClientWrapper';

// Test component that uses useQuery to verify QueryClient is provided
function TestQueryComponent() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ['test'],
    queryFn: async () => {
      return { message: 'test data' };
    },
  });

  if (isLoading) return <div>Loading...</div>;
  if (isError) return <div>Error occurred</div>;
  return <div>{data?.message}</div>;
}

// Test component that triggers a query failure to test retry behavior
function FailingQueryComponent({ queryFn }: { queryFn: () => Promise<unknown> }) {
  const { isLoading, isError, failureCount } = useQuery({
    queryKey: ['failing-test'],
    queryFn,
  });

  if (isLoading) return <div>Loading...</div>;
  if (isError)
    return (
      <div data-testid="error-state">Error (attempts: {failureCount + 1})</div>
    );
  return <div>Success</div>;
}

describe('QueryClientWrapper', () => {
  describe('Rendering', () => {
    it('should render children correctly', () => {
      render(
        <QueryClientWrapper>
          <div data-testid="child-element">Test Child</div>
        </QueryClientWrapper>,
      );

      expect(screen.getByTestId('child-element')).toBeVisible();
      expect(screen.getByText('Test Child')).toBeInTheDocument();
    });

    it('should render multiple children', () => {
      render(
        <QueryClientWrapper>
          <div data-testid="child-1">First Child</div>
          <div data-testid="child-2">Second Child</div>
        </QueryClientWrapper>,
      );

      expect(screen.getByTestId('child-1')).toBeVisible();
      expect(screen.getByTestId('child-2')).toBeVisible();
    });
  });

  describe('QueryClient provision', () => {
    it('should provide QueryClient to children using useQuery', async () => {
      render(
        <QueryClientWrapper>
          <TestQueryComponent />
        </QueryClientWrapper>,
      );

      // Should start with loading state
      expect(screen.getByText('Loading...')).toBeInTheDocument();

      // Should resolve and display data
      await waitFor(() => {
        expect(screen.getByText('test data')).toBeInTheDocument();
      });
    });

    it('should allow multiple components to use queries', async () => {
      function MultiQueryComponent() {
        const query1 = useQuery({
          queryKey: ['query1'],
          queryFn: async () => ({ data: 'first' }),
        });

        const query2 = useQuery({
          queryKey: ['query2'],
          queryFn: async () => ({ data: 'second' }),
        });

        if (query1.isLoading || query2.isLoading)
          return <div>Loading...</div>;

        return (
          <div>
            <span>{query1.data?.data}</span>
            <span>{query2.data?.data}</span>
          </div>
        );
      }

      render(
        <QueryClientWrapper>
          <MultiQueryComponent />
        </QueryClientWrapper>,
      );

      await waitFor(() => {
        expect(screen.getByText('first')).toBeInTheDocument();
        expect(screen.getByText('second')).toBeInTheDocument();
      });
    });
  });

  describe('QueryClient configuration', () => {
    it(
      'should have retry configuration set',
      async () => {
        const mockQueryFn = vi.fn(async () => {
          throw new Error('Query failed');
        });

        render(
          <QueryClientWrapper>
            <FailingQueryComponent queryFn={mockQueryFn} />
          </QueryClientWrapper>,
        );

        // Wait for query to fail after retries
        await waitFor(
          () => {
            const errorElement = screen.getByTestId('error-state');
            expect(errorElement).toBeInTheDocument();
          },
          { timeout: 8000 },
        );

        // Verify the query function was called multiple times (initial + retries)
        // React Query with retry: 1 should result in 2 calls, but failureCount may differ
        expect(mockQueryFn).toHaveBeenCalledTimes(2);
      },
      15000,
    ); // Test timeout of 15 seconds

    it('should not refetch on window focus by default', async () => {
      const queryFn = vi.fn(async () => ({ data: 'test' }));

      function TrackedQueryComponent() {
        const { data, isLoading } = useQuery({
          queryKey: ['tracked'],
          queryFn,
        });

        if (isLoading) return <div>Loading...</div>;
        return <div>{data?.data}</div>;
      }

      render(
        <QueryClientWrapper>
          <TrackedQueryComponent />
        </QueryClientWrapper>,
      );

      // Wait for initial query to complete
      await waitFor(() => {
        expect(screen.getByText('test')).toBeInTheDocument();
      });

      // Verify initial call
      expect(queryFn).toHaveBeenCalledTimes(1);

      // Simulate window focus event wrapped in act
      await act(async () => {
        window.dispatchEvent(new Event('focus'));
        // Wait a bit to ensure no refetch occurs
        await new Promise((resolve) => setTimeout(resolve, 500));
      });

      // Should still be only 1 call (no refetch on focus)
      expect(queryFn).toHaveBeenCalledTimes(1);
    });
  });

  describe('Edge cases', () => {
    it('should handle empty children gracefully', () => {
      const { container } = render(<QueryClientWrapper>{null}</QueryClientWrapper>);
      // When children is null, QueryClientProvider still renders a wrapper
      expect(container).toBeInTheDocument();
    });

    it('should handle queries with different staleTime settings', async () => {
      function CustomStaleTimeComponent() {
        const { data, isLoading } = useQuery({
          queryKey: ['custom-stale'],
          queryFn: async () => ({ value: 'custom' }),
          staleTime: 10000, // Override default with custom staleTime
        });

        if (isLoading) return <div>Loading...</div>;
        return <div>{data?.value}</div>;
      }

      render(
        <QueryClientWrapper>
          <CustomStaleTimeComponent />
        </QueryClientWrapper>,
      );

      await waitFor(() => {
        expect(screen.getByText('custom')).toBeInTheDocument();
      });
    });
  });
});
