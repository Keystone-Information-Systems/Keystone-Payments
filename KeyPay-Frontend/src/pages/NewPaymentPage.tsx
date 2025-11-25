import { useEffect, useState, useRef } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getPaymentMethods, cancelPayment } from '@/services/paymentService';
import AdyenDropin from '@/components/payment/AdyenDropin';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { formatCurrency } from '@/utils/formatters';
import { Box, Typography, List, ListItem, ListItemText, TextField, Divider, Alert, Paper, Button, Stack, Backdrop, CircularProgress } from '@mui/material';
import PersonOutlineIcon from '@mui/icons-material/PersonOutline';
import ReceiptLongIcon from '@mui/icons-material/ReceiptLong';
import { exchangeAuthCode } from '@/services/authService';
import { getAuthToken, setAuthToken } from '@/stores/auth';
import { getClientKey as getStoredClientKey, setClientKey as saveClientKey } from '@/stores/clientKey';

export default function NewPaymentPage() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const orderId = sp.get('orderId');
  const code = sp.get('code');
  const [clientKey, setClientKey] = useState<string | undefined>(undefined);
  const [ready, setReady] = useState<boolean>(false);

  // Hooks must be called unconditionally and before any early returns
  const [holderName, setHolderName] = useState<string>('');
  const [showNameWarning, setShowNameWarning] = useState<boolean>(false);
  const [cancelLoading, setCancelLoading] = useState(false);
  const [cancelError, setCancelError] = useState<string | null>(null);
  const [processing, setProcessing] = useState<boolean>(false);
  // Surcharge-related state must be declared before any early returns to keep hook order stable
  const [surchargeMinor] = useState<number>(0);
  // estimation removed; do not use state
  // Must be declared before any early returns (hooks order)
  const totalToSendMinorRef = useRef<number>(0);

  useEffect(() => {
    (async () => {
      if (!orderId) { navigate('/'); return; }
      // If we have a code, exchange it before making secured calls
      try {
        if (code) {
          const { token, adyenClientKey } = await exchangeAuthCode(code);
          setAuthToken(token);
          setClientKey(adyenClientKey);
          saveClientKey(adyenClientKey || null);
          // Clean URL: remove code param
          const url = new URL(window.location.href);
          url.searchParams.delete('code');
          window.history.replaceState({}, '', url.toString());
        } else {
          // Initialize clientKey from store if present; otherwise wait for server data
          const stored = getStoredClientKey();
          if (stored) {
            setClientKey(stored);
          }
        }
      } catch (e) {
        console.error('Auth code exchange failed', e);
      } finally {
        setReady(true);
      }
    })();
  }, [orderId, code, navigate]);
  
  const { data, isLoading, error } = useQuery({
    queryKey: ['pm', orderId],
    queryFn: () => getPaymentMethods({ orderId: orderId! }),
    enabled: !!orderId && !!getAuthToken() && ready
  });

  // Sync holder name from server once data loads (handle null/undefined)
  useEffect(() => {
    if (data?.cardHolderName != null) {
      setHolderName(data.cardHolderName || '');
    }
  }, [data?.cardHolderName]);

  // Keep latest total to send (base + surcharge) without changing hooks order
  useEffect(() => {
    if (!data) return;
    const lineItemsLocal = data.lineItems ?? [];
    const itemsTotalMinorLocal = lineItemsLocal.reduce((sum: number, li: any) => sum + (li?.amountValue ?? 0), 0);
    const baseTotalMinorLocal = (data.amount?.value ?? itemsTotalMinorLocal);
    const backendSurcharge = (data as any)?.surcharge?.amount ?? 0;
    totalToSendMinorRef.current = baseTotalMinorLocal + (surchargeMinor || backendSurcharge);
  }, [data?.amount?.value, data?.lineItems, surchargeMinor]);

  if (!orderId) return null;
  if (isLoading) return <LoadingSpinner size="lg" message="Loading payment methods..." />;

  if (error || !data) {
    return (
      <div className="p-6">
        <p className="text-red-600">Failed to load payment methods.</p>
        <button className="btn-primary" onClick={() => navigate('/')}>Back</button>
      </div>
    );
  }

  // Wait until a valid clientKey is available before rendering Drop-in
  if (!clientKey) {
    return <LoadingSpinner size="lg" message="Preparing secure payment session..." />;
  }

  const { paymentMethods, reference, amount, countryCode, lineItems = [], username, email, transactionId, surcharge } = data;
  const legacyPostUrl: string | undefined = (data as any)?.legacyPostUrl;
  const itemsTotalMinor = lineItems.reduce((sum: number, li: any) => sum + (li?.amountValue ?? 0), 0);
  const baseTotalMinor = amount?.value ?? itemsTotalMinor;
  // Use backend-provided surcharge amount if available
  const initialSurchargeMinor = surcharge?.amount ?? 0;
  const totalToPayMinor = baseTotalMinor + (surchargeMinor || initialSurchargeMinor);
  const holderNameError = !holderName.trim();
  const showError = showNameWarning && holderNameError;

  const existingTransactionId = transactionId as string;

  return (
    <div className="max-w-2xl mx-auto p-6">
      <Backdrop open={processing} sx={{ color: '#fff', zIndex: (theme) => theme.zIndex.modal + 1 }}>
        <Stack alignItems="center" spacing={2}>
          <CircularProgress color="inherit" />
          <Typography variant="body1">Processing payment… Please don’t close this window.</Typography>
        </Stack>
      </Backdrop>
      <Stack direction="row" justifyContent="space-between" alignItems="center" mb={2}>
        <Typography variant="h5">Checkout</Typography>
        <Button
          variant="outlined"
          color="error"
          size="small"
          disabled={cancelLoading}
          onClick={async () => {
            if (!existingTransactionId) { navigate('/'); return; }
            if (!window.confirm('Cancel this payment?')) return;
            try {
              setCancelLoading(true); setCancelError(null);
              await cancelPayment(existingTransactionId);
              // Load legacyPostUrl and surcharge to build cancel POST
              const pm = await getPaymentMethods({ orderId: reference });
              const legacyUrl: string | undefined = (pm as any)?.legacyPostUrl;
              if (legacyUrl) {
                const inputVal = [
                  'Cancelled',
                  reference,
                  existingTransactionId,
                  '',
                  String(baseTotalMinor + (pm?.surcharge?.amount ?? 0)),
                  amount.currency,
                  String(pm?.surcharge?.amount ?? 0),
                  new Date().toISOString()
                ].join('|');
                const form = document.createElement('form');
                form.method = 'POST';
                form.action = legacyUrl;
                form.style.display = 'none';
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = 'payload';
                input.value = inputVal;
                form.appendChild(input);
                document.body.appendChild(form);
                form.submit();
                return;
              }
              navigate('/');
            } catch (e: any) {
              setCancelError(e?.message || 'Cancel failed');
            } finally {
              setCancelLoading(false);
            }
          }}
        >
          {cancelLoading ? 'Cancelling…' : 'Cancel'}
        </Button>
      </Stack>
      {cancelError && <Alert severity="error" className="mb-3">{cancelError}</Alert>}
      {(username || email) && (
        <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
          <Box display="flex" alignItems="center" gap={1.5}>
            <PersonOutlineIcon color="primary" />
            <Box>
              {username && (<Typography variant="subtitle1" className="font-medium">{username}</Typography>)}
              {email && (<Typography variant="body2" color="text.secondary">{email}</Typography>)}
            </Box>
          </Box>
        </Paper>
      )}

      {lineItems.length > 0 && (
        <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
          <Box display="flex" alignItems="center" justifyContent="space-between" mb={1}>
            <Box display="flex" alignItems="center" gap={1}>
              <ReceiptLongIcon color="primary" />
              <Typography variant="h6">Items</Typography>
            </Box>
          </Box>
          <Divider className="my-2" />
          <List dense>
            {lineItems.map((li: any, idx: number) => (
              <ListItem key={`${li.billNumber || idx}-${idx}`} divider secondaryAction={
                <Typography variant="body2" sx={{ minWidth: 96, textAlign: 'right' }}>
                  {formatCurrency(li.amountValue, amount.currency)}
                </Typography>
              }>
                <ListItemText
                  primary={`${li.description} (Acct: ${li.accountNumber})`}
                  secondary={li.billNumber ? `Bill: ${li.billNumber}` : undefined}
                />
              </ListItem>
            ))}
          </List>
          <Divider className="my-2" />
          <List dense>
            <ListItem
              secondaryAction={
                <Box sx={{ minWidth: 140, textAlign: 'right' }}>
                  <Typography variant="body2">
                    {formatCurrency(baseTotalMinor, amount.currency)}
                  </Typography>
                </Box>
              }
            >
              <ListItemText primary="Invoice total" />
            </ListItem>
            <ListItem
              secondaryAction={
                <Box sx={{ minWidth: 140, textAlign: 'right' }}>
                  <Typography variant="body2">
                    {formatCurrency(initialSurchargeMinor, amount.currency)}
                  </Typography>
                </Box>
              }
            >
              <ListItemText primary="Surcharge fee" />
            </ListItem>
            <ListItem
              secondaryAction={
                <Box sx={{ minWidth: 140, textAlign: 'right' }}>
                  <Typography variant="h6" sx={{ fontWeight: 700 }}>
                    {formatCurrency(totalToPayMinor, amount.currency)}
                  </Typography>
                </Box>
              }
            >
              <ListItemText primary="Total" primaryTypographyProps={{ variant: 'h6', fontWeight: 700 }} />
            </ListItem>
          </List>
        </Paper>
      )}

      <Box mb={2}>
        <TextField
          fullWidth
          required
          label="Card holder name"
          placeholder="Enter name as it appears on card"
          value={holderName}
          onChange={(e) => setHolderName(e.target.value)}
          error={showError}
          helperText={showError ? 'Please enter the card holder name' : ' '}
        />
      </Box>
      <AdyenDropin
        paymentMethodsResponse={paymentMethods}
        reference={reference}
        amount={amount}
        transactionId={existingTransactionId}
        getSubmitAmount={() => ({ value: totalToPayMinor, currency: amount.currency })}
        countryCode={countryCode}
        clientKey={clientKey}
        cardHolderName={holderName || undefined}
        onRequireHolderName={() => setShowNameWarning(true)}
        onProcessingChange={setProcessing}
        onFinalResult={(r) => {
            const rc = (r.resultCode || '').toLowerCase();
            if (r.provisional) {
              const qp = new URLSearchParams();
              if (r.txId) qp.set('transactionId', r.txId);
              if (r.pspReference) qp.set('pspReference', r.pspReference);
              if (reference) qp.set('reference', reference);
              const used = totalToSendMinorRef.current;
              if (used != null) qp.set('amountMinor', String(used));
              if (amount?.currency) qp.set('currency', amount.currency);
              if (r.statusCheckUrl) qp.set('statusCheckUrl', r.statusCheckUrl);
              navigate(`/payment/success?${qp.toString()}`,
                { state: {
                    transactionId: r.txId,
                    pspReference: r.pspReference,
                    reference,
                    amountMinor: used,
                    currency: amount?.currency,
                    statusCheckUrl: r.statusCheckUrl
                  }
                }
              );
            } else if (rc === 'authorised' || rc === 'authorized') {
              // Redirect back to legacy system on Authorised
              if (legacyPostUrl) {
                const used = totalToSendMinorRef.current;
                const totalMinor = used != null ? used : (amount?.value ?? 0);
                const surAmt = (data as any)?.surcharge?.amount ?? 0;
                const inputVal = [
                  'Authorised',
                  reference,
                  r.txId || existingTransactionId,
                  r.pspReference || '',
                  String(totalMinor),
                  amount?.currency,
                  String(surAmt),
                  new Date().toISOString()
                ].join('|');
                const form = document.createElement('form');
                form.method = 'POST';
                form.action = legacyPostUrl;
                form.style.display = 'none';
                const input = document.createElement('input');
                input.type = 'hidden';
                input.name = 'payload';
                input.value = inputVal;
                form.appendChild(input);
                // add explicit fields for downstream parsing
                const pmType = document.createElement('input');
                pmType.type = 'hidden';
                pmType.name = 'paymentMethodType';
                pmType.value = (r as any)?.paymentMethodType || '';
                form.appendChild(pmType);
                const pmBrand = document.createElement('input');
                pmBrand.type = 'hidden';
                pmBrand.name = 'paymentMethodBrand';
                pmBrand.value = (r as any)?.paymentMethodBrand || '';
                form.appendChild(pmBrand);
                const chnInput = document.createElement('input');
                chnInput.type = 'hidden';
                chnInput.name = 'cardHolderName';
                chnInput.value = (r as any)?.cardHolderName || holderName || '';
                form.appendChild(chnInput);
                document.body.appendChild(form);
                form.submit();
                return;
              }
              // Fallback to internal success page if no legacyPostUrl
              const qp = new URLSearchParams();
              if (r.txId) qp.set('transactionId', r.txId);
              if (r.pspReference) qp.set('pspReference', r.pspReference);
              if (reference) qp.set('reference', reference);
              const used = totalToSendMinorRef.current;
              if (used != null) qp.set('amountMinor', String(used));
              if (amount?.currency) qp.set('currency', amount.currency);
              const qs = qp.toString();
              navigate(`/payment/success${qs ? `?${qs}` : ''}`, { state: { transactionId: r.txId, pspReference: r.pspReference, reference, amountMinor: used, currency: amount?.currency } });
            } else {
              const qp = new URLSearchParams();
              if (r.txId) qp.set('transactionId', r.txId);
              if (reference) qp.set('reference', reference);
              navigate(`/payment/error?reason=${encodeURIComponent(rc || 'refused')}&${qp.toString()}`);
            }
        }}
        onError={(e) => {
            console.error('Adyen error', e);
            const qp = new URLSearchParams();
            if (existingTransactionId) qp.set('transactionId', existingTransactionId);
            if (reference) qp.set('reference', reference);
            navigate(`/payment/error?reason=failed${qp.toString() ? `&${qp.toString()}` : ''}`);
        }}
      />
    </div>
  );
}