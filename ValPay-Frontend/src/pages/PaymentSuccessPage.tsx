import React from 'react';
import { useSearchParams, useNavigate, useLocation } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getTransactionStatus, getStatusByUrl } from '@/services/paymentService';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { formatCurrency } from '@/utils/formatters';

export const PaymentSuccessPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  
  const location = useLocation() as { state?: { transactionId?: string; reference?: string; amountMinor?: number; currency?: string; pspReference?: string } };
  const stateTxId = location.state?.transactionId;
  const stateRef = location.state?.reference;
  const stateAmt = location.state?.amountMinor;
  const stateCur = location.state?.currency;

  const transactionId = searchParams.get('transactionId') || stateTxId || undefined as unknown as string;
  const sessionId = searchParams.get('sessionId');
  const statusCheckUrl = searchParams.get('statusCheckUrl') || (location.state as any)?.statusCheckUrl || null;
  const qpAmountMinor = searchParams.get('amountMinor') || (stateAmt != null ? String(stateAmt) : null);
  const qpCurrency = searchParams.get('currency') || stateCur || null;
  const qpReference = searchParams.get('reference') || stateRef || null;

  const {
    data: transaction,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['transaction', transactionId],
    queryFn: () => getTransactionStatus(transactionId!),
    enabled: !!transactionId && !statusCheckUrl,
    refetchInterval: (data) => data?.status === 'Pending' ? 2000 : false,
  });

  // Provisional flow: optimistic success and poll statusCheckUrl with backoff 1s->2s->4s... up to ~60s
  const [provisionalTx, setProvisionalTx] = React.useState<any>(statusCheckUrl ? { status: 'Authorised' } : null);
  React.useEffect(() => {
    if (!statusCheckUrl) return;
    let cancelled = false;
    const terminal = new Set(['Authorised', 'Refused', 'Captured', 'Cancelled', 'Refunded']);
    const delays = [1000, 2000, 4000, 8000, 16000, 32000];
    let attempt = 0;
    const poll = async () => {
      try {
        const data = await getStatusByUrl(statusCheckUrl);
        if (cancelled) return;
        setProvisionalTx(data);
        if (terminal.has((data?.status as string) || '')) return;
      } catch {
        // ignore transient errors and continue backoff
      }
      const delay = delays[Math.min(attempt, delays.length - 1)];
      attempt++;
      if (!cancelled) setTimeout(poll, delay);
    };
    poll();
    return () => { cancelled = true; };
  }, [statusCheckUrl]);

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Authorised':
      case 'Captured':
        return 'bg-green-100 text-green-800 border-green-200';
      case 'Refused':
        return 'bg-red-100 text-red-800 border-red-200';
      case 'Cancelled':
        return 'bg-gray-100 text-gray-800 border-gray-200';
      case 'Pending':
        return 'bg-yellow-100 text-yellow-800 border-yellow-200';
      default:
        return 'bg-gray-100 text-gray-800 border-gray-200';
    }
  };

  const getStatusIcon = (status: string) => {
    switch (status) {
      case 'Authorised':
      case 'Captured':
        return '✅';
      case 'Refused':
        return '❌';
      case 'Cancelled':
        return '⚠️';
      case 'Pending':
        return '⏳';
      default:
        return '❓';
    }
  };

  if (!statusCheckUrl && isLoading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <LoadingSpinner size="lg" message="Loading transaction details..." />
      </div>
    );
  }

  const effectiveTransaction: any = statusCheckUrl ? provisionalTx : transaction;
  if (error || !effectiveTransaction) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center p-4">
        <div className="max-w-md w-full">
          <div className="bg-white rounded-lg shadow-sm border border-red-200 p-6">
            <div className="flex items-center mb-4">
              <div className="flex-shrink-0">
                <svg className="w-6 h-6 text-red-600" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-2.5L13.732 4c-.77-.833-1.964-.833-2.732 0L3.732 16.5c-.77.833.192 2.5 1.732 2.5z" />
                </svg>
              </div>
              <div className="ml-3">
                <h3 className="text-lg font-medium text-red-900">
                  Failed to Load Transaction
                </h3>
              </div>
            </div>
            <p className="text-sm text-red-700 mb-4">
              {error instanceof Error ? error.message : 'Unknown error occurred'}
            </p>
            <button
              onClick={() => navigate('/')}
              className="btn-primary"
            >
              Return to Home
            </button>
          </div>
        </div>
      </div>
    );
  }

  const isSuccess = effectiveTransaction.status === 'Authorised' || effectiveTransaction.status === 'Captured';
  // Safe display mappings (handle varying backend shapes)
  const displayAmountMinor: number | undefined =
    (qpAmountMinor ? Number(qpAmountMinor) : undefined) ??
    effectiveTransaction.amountValue ?? effectiveTransaction.amount?.value ?? effectiveTransaction.amountMinor;
  const displayCurrency: string | undefined =
    qpCurrency ?? effectiveTransaction.currencyCode ?? effectiveTransaction.amount?.currency ?? effectiveTransaction.currency;
  const displayTransactionId: string | undefined =
    effectiveTransaction.transactionId ?? effectiveTransaction.txId ?? stateTxId ?? effectiveTransaction.id ?? undefined;
  const displayOrderRef: string | undefined =
    qpReference ?? effectiveTransaction.merchantReference ?? effectiveTransaction.reference ?? effectiveTransaction.orderReference;

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="text-center mb-8">
          <div className="text-6xl mb-4">
            {getStatusIcon(effectiveTransaction.status)}
          </div>
          <h1 className="text-3xl font-bold text-gray-900 mb-2">
            {isSuccess ? 'Payment Successful!' : 'Payment Status'}
          </h1>
          <p className="text-gray-600">
            {isSuccess 
              ? 'Your payment has been processed successfully.'
              : 'Here are the details of your payment attempt.'
            }
          </p>
        </div>

        {/* Transaction Details */}
        <div className="card mb-6">
          <h2 className="text-xl font-semibold text-gray-900 mb-6">
            Transaction Details
          </h2>
          
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
            <div>
              <p className="text-sm font-medium text-gray-500 mb-1">Amount</p>
              <p className="text-2xl font-bold text-primary-600">
                {typeof displayAmountMinor === 'number' && displayCurrency
                  ? formatCurrency(displayAmountMinor, displayCurrency)
                  : '-'}
              </p>
            </div>
            
            <div>
              <p className="text-sm font-medium text-gray-500 mb-1">Status</p>
              <span className={`inline-flex items-center px-3 py-1 rounded-full text-sm font-medium border ${getStatusColor(effectiveTransaction.status)}`}>
                {effectiveTransaction.status}
              </span>
            </div>

            <div>
              <p className="text-sm font-medium text-gray-500 mb-1">Transaction ID</p>
              <p className="text-sm font-mono text-gray-900 bg-gray-50 px-2 py-1 rounded">
                {displayTransactionId || '-'}
              </p>
            </div>

            <div>
              <p className="text-sm font-medium text-gray-500 mb-1">Order Reference</p>
              <p className="text-sm font-mono text-gray-900 bg-gray-50 px-2 py-1 rounded">
                {displayOrderRef || '-'}
              </p>
            </div>

            {effectiveTransaction.pspReference && (
              <div>
                <p className="text-sm font-medium text-gray-500 mb-1">PSP Reference</p>
                <p className="text-sm font-mono text-gray-900 bg-gray-50 px-2 py-1 rounded">
                  {effectiveTransaction.pspReference}
                </p>
              </div>
            )}

            {effectiveTransaction.resultCode && (
              <div>
                <p className="text-sm font-medium text-gray-500 mb-1">Result Code</p>
                <p className="text-sm text-gray-900">
                  {effectiveTransaction.resultCode}
                </p>
              </div>
            )}
          </div>

          {effectiveTransaction.refusalReason && (
            <div className="mt-6 p-4 bg-yellow-50 border border-yellow-200 rounded-lg">
              <h3 className="text-sm font-medium text-yellow-800 mb-1">
                Refusal Reason
              </h3>
              <p className="text-sm text-yellow-700">
                {effectiveTransaction.refusalReason}
              </p>
            </div>
          )}
        </div>

        {/* Session Info */}
        {sessionId && (
          <div className="card mb-6">
            <h2 className="text-xl font-semibold text-gray-900 mb-4">
              Session Information
            </h2>
            <p className="text-sm text-gray-600">
              Session ID: <span className="font-mono">{sessionId}</span>
            </p>
          </div>
        )}

        {/* Actions */}
        <div className="flex flex-col sm:flex-row gap-4 justify-center">
          <button
            onClick={() => navigate('/')}
            className="btn-primary"
          >
            New Payment
          </button>
          
          <button
            onClick={() => window.print()}
            className="btn-secondary"
          >
            Print Receipt
          </button>

          {isSuccess && (
            <button
              onClick={() => {
                const details = `Transaction ID: ${effectiveTransaction.transactionId}\nAmount: ${formatCurrency(effectiveTransaction.amountValue, effectiveTransaction.currencyCode)}\nStatus: ${effectiveTransaction.status}`;
                navigator.clipboard.writeText(details);
                alert('Transaction details copied to clipboard!');
              }}
              className="btn-secondary"
            >
              Copy Details
            </button>
          )}
        </div>

        {/* Test Mode Notice */}
        <div className="mt-8">
          <div className="bg-blue-50 border border-blue-200 rounded-lg p-4">
            <div className="flex">
              <div className="flex-shrink-0">
                <svg className="w-5 h-5 text-blue-400" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
                </svg>
              </div>
              <div className="ml-3">
                <h3 className="text-sm font-medium text-blue-800">
                  Test Mode
                </h3>
                <p className="mt-1 text-sm text-blue-700">
                  This was a test transaction. No real money was charged.
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};