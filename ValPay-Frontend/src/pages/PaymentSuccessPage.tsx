import React from 'react';
import { useSearchParams, useLocation } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getTransactionStatus, getStatusByUrl, getPaymentMethods } from '@/services/paymentService';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
// import { formatCurrency } from '@/utils/formatters';

export const PaymentSuccessPage: React.FC = () => {
  const [searchParams] = useSearchParams();
  // const navigate = useNavigate();
  
  const location = useLocation() as { state?: { transactionId?: string; reference?: string; amountMinor?: number; currency?: string; pspReference?: string } };
  const stateTxId = location.state?.transactionId;
  const stateRef = location.state?.reference;
  const stateAmt = location.state?.amountMinor;
  const stateCur = location.state?.currency;

  const transactionId = searchParams.get('transactionId') || stateTxId || undefined as unknown as string;
  // const sessionId = searchParams.get('sessionId');
  const statusCheckUrl = searchParams.get('statusCheckUrl') || (location.state as any)?.statusCheckUrl || null;
  const qpAmountMinor = searchParams.get('amountMinor') || (stateAmt != null ? String(stateAmt) : null);
  const qpCurrency = searchParams.get('currency') || stateCur || null;
  const qpReference = searchParams.get('reference') || stateRef || null;

  const {
    data: transaction,
    isLoading,
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

  // Fetch legacyPostUrl and surcharge using the original reference (orderId)
  const { data: pmData } = useQuery({
    queryKey: ['pmData-success', qpReference],
    queryFn: () => getPaymentMethods({ orderId: qpReference! }),
    enabled: !!qpReference
  });

  // Auto form-POST to legacy on final success
  React.useEffect(() => {
    const final: any = statusCheckUrl ? provisionalTx : transaction;
    const st = (final?.status as string | undefined)?.toLowerCase();
    if (!st || !pmData?.legacyPostUrl) return;
    if (st !== 'authorised' && st !== 'captured') return;

    const legacyUrl = pmData.legacyPostUrl;
    const payload = {
      status: final?.status as string,
      legacyReference: qpReference || stateRef || '',
      paymentId: transactionId || '',
      pspReference: (final?.pspReference || (location.state as any)?.pspReference || '') as string,
      amount: Number(qpAmountMinor || 0),
      currency: (qpCurrency || '') as string,
      surchargeAmount: pmData?.surcharge?.amount ?? 0,
      timestamp: new Date().toISOString()
    };

    try {
      const form = document.createElement('form');
      form.method = 'POST';
      form.action = legacyUrl;
      form.style.display = 'none';
      const input = document.createElement('input');
      input.type = 'hidden';
      input.name = 'payload';
      const pipe = [
        payload.status,
        payload.legacyReference,
        payload.paymentId,
        payload.pspReference,
        String(payload.amount),
        payload.currency,
        String(payload.surchargeAmount),
        payload.timestamp
      ].join('|');
      input.value = pipe;
      form.appendChild(input);
      document.body.appendChild(form);
      form.submit();
    } catch {}
  }, [transaction, provisionalTx, pmData?.legacyPostUrl]);

  // helper placeholders removed (no UI rendering on success page)

  // helper placeholders removed (no UI rendering on success page)

  if (!statusCheckUrl && isLoading) {
    return (
      <div className="min-h-screen bg-gray-50 flex items-center justify-center">
        <LoadingSpinner size="lg" message="Finalising payment..." />
      </div>
    );
  }

  // const effectiveTransaction: any = statusCheckUrl ? provisionalTx : transaction;
  // Minimal spinner-only page; redirects are handled via effect once success is finalised.
  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center">
      <LoadingSpinner size="lg" message="Finalising payment..." />
    </div>
  );
};