import { useEffect, useState } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { getPaymentMethods, cancelPayment, estimatePaymentCost } from '@/services/paymentService';
import AdyenDropin from '@/components/payment/AdyenDropin';
import { LoadingSpinner } from '@/components/ui/LoadingSpinner';
import { formatCurrency } from '@/utils/formatters';
import { Box, Typography, List, ListItem, ListItemText, TextField, Divider, Alert, Paper, Button, Stack } from '@mui/material';
import PersonOutlineIcon from '@mui/icons-material/PersonOutline';
import ReceiptLongIcon from '@mui/icons-material/ReceiptLong';

export default function NewPaymentPage() {
  const [sp] = useSearchParams();
  const navigate = useNavigate();
  const orderId = sp.get('orderId');

  // Hooks must be called unconditionally and before any early returns
  const [holderName, setHolderName] = useState<string>('');
  const [showNameWarning, setShowNameWarning] = useState<boolean>(false);
  const [cancelLoading, setCancelLoading] = useState(false);
  const [cancelError, setCancelError] = useState<string | null>(null);
  // Surcharge-related state must be declared before any early returns to keep hook order stable
  const [surchargeMinor, setSurchargeMinor] = useState<number>(0);
  const [estimating, setEstimating] = useState<boolean>(false);

  useEffect(() => {
    if (!orderId) navigate('/'); // prevent loops via router config
  }, [orderId, navigate]);
  
  const { data, isLoading, error } = useQuery({
    queryKey: ['pm', orderId],
    queryFn: () => getPaymentMethods({ orderId: orderId! }),
    enabled: !!orderId
  });

  // Sync holder name from server once data loads (handle null/undefined)
  useEffect(() => {
    if (data?.cardHolderName != null) {
      setHolderName(data.cardHolderName || '');
    }
  }, [data?.cardHolderName]);

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

  const { paymentMethods, reference, amount, countryCode, lineItems = [], username, email, transactionId } = data;
  const itemsTotalMinor = lineItems.reduce((sum: number, li: any) => sum + (li?.amountValue ?? 0), 0);
  const baseTotalMinor = amount?.value ?? itemsTotalMinor;
  const totalToPayMinor = baseTotalMinor + surchargeMinor;
  const holderNameError = !holderName.trim();
  const showError = showNameWarning && holderNameError;

  const existingTransactionId = transactionId as string;

  return (
    <div className="max-w-2xl mx-auto p-6">
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
            <Typography variant="subtitle1" className="font-semibold">
              {formatCurrency(totalToPayMinor, amount.currency)}
            </Typography>
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
                    {formatCurrency(surchargeMinor, amount.currency)}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {surchargeMinor === 0 && !estimating ? 'Will be calculated after card number' : estimating ? 'Estimating…' : ''}
                  </Typography>
                </Box>
              }
            >
              <ListItemText primary="Surcharge fee" />
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
        countryCode={countryCode}
        cardHolderName={holderName || undefined}
        onRequireHolderName={() => setShowNameWarning(true)}
        onFinalResult={(r) => {
            const rc = (r.resultCode || '').toLowerCase();
            if (r.provisional) {
              const qp = new URLSearchParams();
              if (r.txId) qp.set('transactionId', r.txId);
              if (r.pspReference) qp.set('pspReference', r.pspReference);
              if (reference) qp.set('reference', reference);
              if (amount?.value != null) qp.set('amountMinor', String(amount.value));
              if (amount?.currency) qp.set('currency', amount.currency);
              if (r.statusCheckUrl) qp.set('statusCheckUrl', r.statusCheckUrl);
              navigate(`/payment/success?${qp.toString()}`,
                { state: {
                    transactionId: r.txId,
                    pspReference: r.pspReference,
                    reference,
                    amountMinor: amount?.value,
                    currency: amount?.currency,
                    statusCheckUrl: r.statusCheckUrl
                  }
                }
              );
            } else if (rc === 'authorised' || rc === 'authorized') {
              const qp = new URLSearchParams();
              if (r.txId) qp.set('transactionId', r.txId);
              if (r.pspReference) qp.set('pspReference', r.pspReference);
              // pass order reference and amount context as fallbacks for the success page
              if (reference) qp.set('reference', reference);
              if (amount?.value != null) qp.set('amountMinor', String(amount.value));
              if (amount?.currency) qp.set('currency', amount.currency);
              const qs = qp.toString();
              navigate(`/payment/success${qs ? `?${qs}` : ''}`,
                { state: {
                    transactionId: r.txId,
                    pspReference: r.pspReference,
                    reference,
                    amountMinor: amount?.value,
                    currency: amount?.currency
                  }
                }
              );
            } else {
              navigate(`/payment/error?reason=${rc || 'refused'}`);
            }
        }}
        onError={(e) => {
            console.error('Adyen error', e);
            navigate('/payment/error?reason=failed');
        }}
        onEncryptedCardNumber={async (encryptedCardNumber) => {
            setEstimating(true);
            try {
              const res = await estimatePaymentCost({
                amount,
                encryptedCardNumber,
                reference,
                shopperCountry: countryCode,
                transactionId: existingTransactionId
              });
              setSurchargeMinor(res.surchargeAmount ?? 0);
            } finally {
              setEstimating(false);
            }
        }}
      />
    </div>
  );
}