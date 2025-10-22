import React from 'react';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ErrorBoundary } from '@/components/ui/ErrorBoundary';
import { Header } from '@/components/layout/Header';
import { Footer } from '@/components/layout/Footer';
import NewPaymentPage from '@/pages/NewPaymentPage';
import { PaymentSuccessPage } from '@/pages/PaymentSuccessPage';
import { PaymentErrorPage } from '@/pages/PaymentErrorPage';
import { DemoPage } from '@/pages/DemoPage';
import { NotFoundPage } from '@/pages/NotFoundPage';
import '@/styles/globals.css';

// Create a client
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error: any) => {
        // Don't retry on client errors (4xx)
        if (error.status && error.status >= 400 && error.status < 500) {
          return false;
        }
        // Retry up to 3 times for server errors
        return failureCount < 3;
      },
      retryDelay: (attemptIndex) => Math.min(1000 * 2 ** attemptIndex, 30000),
      staleTime: 5 * 60 * 1000, // 5 minutes
      cacheTime: 10 * 60 * 1000, // 10 minutes
    },
    mutations: {
      retry: false,
    },
  },
});

const App: React.FC = () => {
  return (
    <QueryClientProvider client={queryClient}>
      <Router>
        <div className="min-h-screen flex flex-col bg-gray-50">
          <Header />
          
          <main className="flex-1">
            <ErrorBoundary>
              <Routes>
                <Route path="/" element={<DemoPage />} />
                <Route path="/payment" element={<NewPaymentPage />} />
                <Route path="/payment/success" element={<PaymentSuccessPage />} />
                <Route path="/payment/error" element={<PaymentErrorPage />} />
                <Route path="/payment/cancelled" element={<PaymentErrorPage />} />
                <Route path="/payment/result" element={<PaymentSuccessPage />} />
                <Route path="*" element={<NotFoundPage />} />
              </Routes>
            </ErrorBoundary>
          </main>
          
          <Footer />
        </div>
      </Router>
    </QueryClientProvider>
  );
};

export default App;