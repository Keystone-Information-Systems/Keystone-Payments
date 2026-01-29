import { useState } from 'react';
import { Box, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Link, Stack, TextField, Typography, Button } from '@mui/material';

export type AddressForm = {
  firstName: string;
  lastName: string;
  address: string;
  addressContinued: string;
  city: string;
  state: string;
  zip: string;
  phoneNumber: string;
  emailAddress: string;
  isInternational: boolean;
  termsAccepted: boolean;
};

type Props = {
  value: AddressForm;
  onChange: (next: AddressForm) => void;
  showErrors: boolean;
};

export default function AddressStep({ value, onChange, showErrors }: Props) {
  const [termsOpen, setTermsOpen] = useState(false);

  const update = (patch: Partial<AddressForm>) => onChange({ ...value, ...patch });

  const requiredError = (val: string) => showErrors && !val.trim();

  return (
    <Box>
      <Typography variant="h6" mb={1.5}>Payment Details</Typography>
      <FormControlLabel
        control={
          <Checkbox
            checked={value.isInternational}
            onChange={(e) => update({ isInternational: e.target.checked })}
          />
        }
        label="Check this if card address is international."
      />

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, mt: 2 }}>
        <TextField
          required
          label="First Name"
          value={value.firstName}
          onChange={(e) => update({ firstName: e.target.value })}
          error={requiredError(value.firstName)}
          helperText={requiredError(value.firstName) ? 'First Name is required' : ' '}
        />
        <TextField
          required
          label="Last Name"
          value={value.lastName}
          onChange={(e) => update({ lastName: e.target.value })}
          error={requiredError(value.lastName)}
          helperText={requiredError(value.lastName) ? 'Last Name is required' : ' '}
        />
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, mt: 1 }}>
        <TextField
          required
          label="Address"
          value={value.address}
          onChange={(e) => update({ address: e.target.value })}
          error={requiredError(value.address)}
          helperText={requiredError(value.address) ? 'Address is required' : ' '}
        />
        <TextField
          label="Address Continued"
          value={value.addressContinued}
          onChange={(e) => update({ addressContinued: e.target.value })}
          helperText=" "
        />
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr 1fr' }, gap: 2, mt: 1 }}>
        <TextField
          required
          label="City"
          value={value.city}
          onChange={(e) => update({ city: e.target.value })}
          error={requiredError(value.city)}
          helperText={requiredError(value.city) ? 'City is required' : ' '}
        />
        <TextField
          required
          label="State"
          value={value.state}
          onChange={(e) => update({ state: e.target.value })}
          error={requiredError(value.state)}
          helperText={requiredError(value.state) ? 'State is required' : ' '}
        />
        <TextField
          required
          label="Zip"
          value={value.zip}
          onChange={(e) => update({ zip: e.target.value })}
          error={requiredError(value.zip)}
          helperText={requiredError(value.zip) ? 'Zip is required' : ' '}
        />
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, mt: 1 }}>
        <TextField
          required
          label="Phone Number"
          value={value.phoneNumber}
          onChange={(e) => update({ phoneNumber: e.target.value })}
          error={requiredError(value.phoneNumber)}
          helperText={requiredError(value.phoneNumber) ? 'Phone Number is required' : ' '}
        />
        <TextField
          label="Email Address"
          value={value.emailAddress}
          onChange={(e) => update({ emailAddress: e.target.value })}
          helperText=" "
        />
      </Box>

      <FormControlLabel
        sx={{ mt: 1 }}
        control={
          <Checkbox
            checked={value.termsAccepted}
            onChange={(e) => update({ termsAccepted: e.target.checked })}
          />
        }
        label={
          <span>
            I agree to the{' '}
            <Link component="button" onClick={() => setTermsOpen(true)}>
              Terms and Conditions
            </Link>
          </span>
        }
      />
      {showErrors && !value.termsAccepted && (
        <Typography variant="caption" color="error" display="block" sx={{ mt: 0.5 }}>
          You must accept the Terms and Conditions
        </Typography>
      )}

      <Dialog open={termsOpen} onClose={() => setTermsOpen(false)} maxWidth="sm" fullWidth>
        <DialogTitle>Terms and Conditions</DialogTitle>
        <DialogContent dividers>
          <Stack spacing={2}>
            <Typography variant="body2">
              This secure service is offered by ValPay Payment Systems in agreement with your payment entity.
              All payments are processed immediately, and the payment date and time are equal to the time you
              complete this transaction and receive a confirmation number.
              If your payment is unable to be processed, your payment liability will remain outstanding and
              you will be subject to any applicable penalties or interest.
              These obligations remain your sole responsibility.
              ValPay Payment Systems cannot issue refunds once your payment is processed and you receive a
              confirmation number.
            </Typography>
          </Stack>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setTermsOpen(false)}>Close</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
