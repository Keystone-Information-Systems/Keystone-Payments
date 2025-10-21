import React from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getTransactionStatus, getPaymentMethods, cancelPayment } from '@/services/paymentService';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { formatCurrency } from '@/utils/formatters';

export const PaymentErrorPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  
  const transactionId = searchParams.get('transactionId');
  const sessionId = searchParams.get('sessionId');
  const reason = searchParams.get('reason') || 'unknown';
  const referenceFromQuery = searchParams.get('reference');
  const [cancelLoading, setCancelLoading] = React.useState(false);

  const {
    data: transaction,
    isLoading,
  } = useQuery({
    queryKey: ['transaction', transactionId],
    queryFn: () => getTransactionStatus(transactionId!),
    enabled: !!transactionId,
  });

  const reference = referenceFromQuery || (transaction as any)?.merchantReference || undefined;

  // Load legacyPostUrl for cancel redirect
  const { data: pmData } = useQuery({
    queryKey: ['pmData-error', reference],
    queryFn: () => getPaymentMethods({ orderId: reference! }),
    enabled: !!reference
  });

  const getErrorInfo = (reason: string, transaction?: any) => {
    switch (reason) {
      case 'declined':
      case 'refused':
        return {
          title: reason === 'refused' ? 'Payment Refused' : 'Payment Declined',
          icon: '❌',
          message: reason === 'refused'
            ? 'Your payment was refused by the payment processor.'
            : 'Your payment was declined by the payment processor.',
          details: transaction?.refusalReason || 'The card issuer declined the transaction.',
          severity: 'error' as const,
        };
      case 'failed':
        return {
          title: 'Payment Failed',
          icon: '⚠️',
          message: 'There was an error processing your payment.',
          details: 'Please try again or use a different payment method.',
          severity: 'error' as const,
        };
      case 'timeout':
        return {
          title: 'Payment Timeout',
          icon: '⏰',
          message: 'The payment took too long to process.',
          details: 'Please check your transaction status or try again.',
          severity: 'warning' as const,
        };
      case 'cancelled':
        return {
          title: 'Payment Cancelled',
          icon: '🚫',
          message: 'The payment was cancelled.',
          details: 'You can start a new payment if needed.',
          severity: 'info' as const,
        };
      default:
        return {
          title: 'Payment Error',
          icon: '❓',
          message: 'An unexpected error occurred.',
          details: 'Please try again or contact support.',
          severity: 'error' as const,
        };
    }
  };

  if (isLoading && transactionId) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <LoadingSpinner size="lg" message="Loading transaction details..." />
      </div>
    );
  }

  const errorInfo = getErrorInfo(reason, transaction);

  return (
    <div className="min-h-screen bg-gray-50 py-8">
      <div className="max-w-4xl mx-auto px-4 sm:px-6 lg:px-8">
        {/* Header */}
        <div className="text-center mb-8">
          <div className="text-6xl mb-4">
            {errorInfo.icon}
          </div>
          <h1 className="text-3xl font-bold text-gray-900 mb-2">
            {errorInfo.title}
          </h1>
          <p className="text-gray-600">
            {errorInfo.message}
          </p>
        </div>

        {/* Error Details */}
        <div className={`mb-6 p-4 rounded-lg border ${
          errorInfo.severity === 'error' 
            ? 'bg-red-50 border-red-200'
            : errorInfo.severity === 'warning'
            ? 'bg-yellow-50 border-yellow-200'
            : 'bg-blue-50 border-blue-200'
        }`}>
          <div className="flex">
            <div className="flex-shrink-0">
              <svg className={`w-5 h-5 ${
                errorInfo.severity === 'error' 
                  ? 'text-red-400'
                  : errorInfo.severity === 'warning'
                  ? 'text-yellow-400'
                  : 'text-blue-400'
              }`} fill="currentColor" viewBox="0 0 20 20">
                <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clipRule="evenodd" />
              </svg>
            </div>
            <div className="ml-3">
              <h3 className={`text-sm font-medium ${
                errorInfo.severity === 'error' 
                  ? 'text-red-800'
                  : errorInfo.severity === 'warning'
                  ? 'text-yellow-800'
                  : 'text-blue-800'
              }`}>
                {errorInfo.title}
              </h3>
              <p className={`mt-1 text-sm ${
                errorInfo.severity === 'error' 
                  ? 'text-red-700'
                  : errorInfo.severity === 'warning'
                  ? 'text-yellow-700'
                  : 'text-blue-700'
              }`}>
                {errorInfo.details}
              </p>
            </div>
          </div>
        </div>

        {/* Transaction Details (if available) */}
        {transaction && (
          <div className="card mb-6">
            <h2 className="text-xl font-semibold text-gray-900 mb-6">
              Transaction Details
            </h2>
            
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div>
                <p className="text-sm font-medium text-gray-500 mb-1">Amount</p>
                {(() => {
                  const qpCur = searchParams.get('currency');
                  const displayCurrency = qpCur || (transaction as any)?.currencyCode || (transaction as any)?.amount?.currency || undefined;
                  const displayAmount = (transaction as any)?.amountValue;
                  return (
                    <p className="text-2xl font-bold text-primary-600">
                      {typeof displayAmount === 'number' && displayCurrency
                        ? formatCurrency(displayAmount, displayCurrency)
                        : '-'}
                    </p>
                  );
                })()}
              </div>
              
              <div>
                <p className="text-sm font-medium text-gray-500 mb-1">Status</p>
                <span className="inline-flex items-center px-3 py-1 rounded-full text-sm font-medium border bg-red-100 text-red-800 border-red-200">
                  {transaction.status}
                </span>
              </div>

              <div>
                <p className="text-sm font-medium text-gray-500 mb-1">Transaction ID</p>
                <p className="text-sm font-mono text-gray-900 bg-gray-50 px-2 py-1 rounded">
                  {transaction.transactionId}
                </p>
              </div>

              <div>
                <p className="text-sm font-medium text-gray-500 mb-1">Order Reference</p>
                <p className="text-sm font-mono text-gray-900 bg-gray-50 px-2 py-1 rounded">
                  {transaction.merchantReference}
                </p>
              </div>

              {transaction.resultCode && (
                <div className="md:col-span-2">
                  <p className="text-sm font-medium text-gray-500 mb-1">Result Code</p>
                  <p className="text-sm text-gray-900">
                    {transaction.resultCode}
                  </p>
                </div>
              )}

              {transaction.refusalReason && (
                <div className="md:col-span-2">
                  <div className="p-4 bg-yellow-50 border border-yellow-200 rounded-lg">
                    <h3 className="text-sm font-medium text-yellow-800 mb-1">
                      Refusal Reason
                    </h3>
                    <p className="text-sm text-yellow-700">
                      {transaction.refusalReason}
                    </p>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}

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

        {/* Minimal actions only */}

        {/* Actions */}
        <div className="flex flex-col sm:flex-row gap-4 justify-center">
          <button
            onClick={() => {
              if (reference) navigate(`/payment?orderId=${encodeURIComponent(reference)}`);
              else navigate('/');
            }}
            className="btn-primary"
          >
            Try Again
          </button>

          <button
            disabled={cancelLoading}
            onClick={async () => {
              try {
                setCancelLoading(true);
                if (transactionId) {
                  try { await cancelPayment(transactionId); } catch {}
                }
                const legacyUrl = pmData?.legacyPostUrl;
                if (legacyUrl && reference) {
                  const payload = {
                    status: 'Cancelled',
                    legacyReference: reference,
                    paymentId: transactionId || '',
                    pspReference: transaction?.pspReference || '',
                    amount: transaction?.amountValue || 0,
                    currency: transaction?.currencyCode || '',
                    surchargeAmount: pmData?.surcharge?.amount ?? 0,
                    timestamp: new Date().toISOString()
                  };
                  const form = document.createElement('form');
                  form.method = 'POST';
                  form.action = legacyUrl;
                  form.style.display = 'none';
                  const input = document.createElement('input');
                  input.type = 'hidden';
                  input.name = 'payload';
                  input.value = [
                    payload.status,
                    payload.legacyReference,
                    payload.paymentId,
                    payload.pspReference,
                    String(payload.amount),
                    payload.currency,
                    String(payload.surchargeAmount),
                    payload.timestamp
                  ].join('|');
                  form.appendChild(input);
                  document.body.appendChild(form);
                  form.submit();
                  return;
                }
                navigate('/');
              } finally {
                setCancelLoading(false);
              }
            }}
            className="btn-secondary"
          >
            {cancelLoading ? 'Cancelling…' : 'Cancel'}
          </button>
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
                  This was a test transaction. No real money was involved.
                  Use different test card numbers to simulate various scenarios.
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};