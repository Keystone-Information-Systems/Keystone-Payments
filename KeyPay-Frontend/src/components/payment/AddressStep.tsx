import { useState } from 'react';
import { Box, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Link, Stack, TextField, Typography, Button, MenuItem } from '@mui/material';

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
  termsAccepted: boolean;
};

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PHONE_REGEX = /^\d{10}$/;
const ZIP_REGEX = /^\d{5}(-\d{4})?$/;

const US_STATES = [
  { code: 'AL', name: 'Alabama' },
  { code: 'AK', name: 'Alaska' },
  { code: 'AZ', name: 'Arizona' },
  { code: 'AR', name: 'Arkansas' },
  { code: 'CA', name: 'California' },
  { code: 'CO', name: 'Colorado' },
  { code: 'CT', name: 'Connecticut' },
  { code: 'DE', name: 'Delaware' },
  { code: 'DC', name: 'District of Columbia' },
  { code: 'FL', name: 'Florida' },
  { code: 'GA', name: 'Georgia' },
  { code: 'HI', name: 'Hawaii' },
  { code: 'ID', name: 'Idaho' },
  { code: 'IL', name: 'Illinois' },
  { code: 'IN', name: 'Indiana' },
  { code: 'IA', name: 'Iowa' },
  { code: 'KS', name: 'Kansas' },
  { code: 'KY', name: 'Kentucky' },
  { code: 'LA', name: 'Louisiana' },
  { code: 'ME', name: 'Maine' },
  { code: 'MD', name: 'Maryland' },
  { code: 'MA', name: 'Massachusetts' },
  { code: 'MI', name: 'Michigan' },
  { code: 'MN', name: 'Minnesota' },
  { code: 'MS', name: 'Mississippi' },
  { code: 'MO', name: 'Missouri' },
  { code: 'MT', name: 'Montana' },
  { code: 'NE', name: 'Nebraska' },
  { code: 'NV', name: 'Nevada' },
  { code: 'NH', name: 'New Hampshire' },
  { code: 'NJ', name: 'New Jersey' },
  { code: 'NM', name: 'New Mexico' },
  { code: 'NY', name: 'New York' },
  { code: 'NC', name: 'North Carolina' },
  { code: 'ND', name: 'North Dakota' },
  { code: 'OH', name: 'Ohio' },
  { code: 'OK', name: 'Oklahoma' },
  { code: 'OR', name: 'Oregon' },
  { code: 'PA', name: 'Pennsylvania' },
  { code: 'RI', name: 'Rhode Island' },
  { code: 'SC', name: 'South Carolina' },
  { code: 'SD', name: 'South Dakota' },
  { code: 'TN', name: 'Tennessee' },
  { code: 'TX', name: 'Texas' },
  { code: 'UT', name: 'Utah' },
  { code: 'VT', name: 'Vermont' },
  { code: 'VA', name: 'Virginia' },
  { code: 'WA', name: 'Washington' },
  { code: 'WV', name: 'West Virginia' },
  { code: 'WI', name: 'Wisconsin' },
  { code: 'WY', name: 'Wyoming' }
];

const VALID_US_STATE_CODES = new Set(US_STATES.map((state) => state.code));

type AddressValidationErrors = {
  firstName: string | null;
  lastName: string | null;
  address: string | null;
  city: string | null;
  state: string | null;
  zip: string | null;
  phoneNumber: string | null;
  emailAddress: string | null;
  termsAccepted: string | null;
};

export const getAddressValidationErrors = (form: AddressForm): AddressValidationErrors => {
  const firstName = form.firstName.trim();
  const lastName = form.lastName.trim();
  const address = form.address.trim();
  const city = form.city.trim();
  const state = form.state.trim().toUpperCase();
  const zip = form.zip.trim();
  const phoneNumber = form.phoneNumber.trim();
  const emailAddress = form.emailAddress.trim();

  return {
    firstName: firstName ? null : 'First Name is required',
    lastName: lastName ? null : 'Last Name is required',
    address: address ? null : 'Address is required',
    city: city ? null : 'City is required',
    state: !state ? 'State is required' : (VALID_US_STATE_CODES.has(state) ? null : 'Please select a valid US state'),
    zip: !zip ? 'Zip is required' : (ZIP_REGEX.test(zip) ? null : 'Zip must be 5 digits or 5-4 format'),
    phoneNumber: !phoneNumber ? 'Phone Number is required' : (PHONE_REGEX.test(phoneNumber) ? null : 'Phone Number must be exactly 10 digits'),
    emailAddress: !emailAddress ? 'Email Address is required' : (EMAIL_REGEX.test(emailAddress) ? null : 'Please enter a valid email address'),
    termsAccepted: form.termsAccepted ? null : 'You must accept the Terms and Conditions'
  };
};

type Props = {
  value: AddressForm;
  onChange: (next: AddressForm) => void;
  showErrors: boolean;
};

export default function AddressStep({ value, onChange, showErrors }: Props) {
  const [termsOpen, setTermsOpen] = useState(false);

  const update = (patch: Partial<AddressForm>) => onChange({ ...value, ...patch });
  const errors = getAddressValidationErrors(value);
  const hasError = (fieldError: string | null) => showErrors && !!fieldError;

  return (
    <Box>
      <Typography variant="h6" mb={1.5}>Payment Details</Typography>
      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, mt: 2 }}>
        <TextField
          required
          label="First Name"
          value={value.firstName}
          onChange={(e) => update({ firstName: e.target.value })}
          error={hasError(errors.firstName)}
          helperText={hasError(errors.firstName) ? errors.firstName : ' '}
        />
        <TextField
          required
          label="Last Name"
          value={value.lastName}
          onChange={(e) => update({ lastName: e.target.value })}
          error={hasError(errors.lastName)}
          helperText={hasError(errors.lastName) ? errors.lastName : ' '}
        />
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, mt: 1 }}>
        <TextField
          required
          label="Address"
          value={value.address}
          onChange={(e) => update({ address: e.target.value })}
          error={hasError(errors.address)}
          helperText={hasError(errors.address) ? errors.address : ' '}
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
          error={hasError(errors.city)}
          helperText={hasError(errors.city) ? errors.city : ' '}
        />
        <TextField
          select
          required
          label="State"
          value={value.state}
          onChange={(e) => update({ state: e.target.value.toUpperCase() })}
          error={hasError(errors.state)}
          helperText={hasError(errors.state) ? errors.state : ' '}
        >
          <MenuItem value="">
            Select a state
          </MenuItem>
          {US_STATES.map((state) => (
            <MenuItem key={state.code} value={state.code}>
              {state.code} - {state.name}
            </MenuItem>
          ))}
        </TextField>
        <TextField
          required
          label="Zip"
          value={value.zip}
          onChange={(e) => update({ zip: e.target.value })}
          error={hasError(errors.zip)}
          helperText={hasError(errors.zip) ? errors.zip : ' '}
          inputProps={{ inputMode: 'numeric', maxLength: 10 }}
        />
      </Box>

      <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '1fr 1fr' }, gap: 2, mt: 1 }}>
        <TextField
          required
          label="Phone Number"
          value={value.phoneNumber}
          onChange={(e) => update({ phoneNumber: e.target.value.replace(/\D/g, '').slice(0, 10) })}
          error={hasError(errors.phoneNumber)}
          helperText={hasError(errors.phoneNumber) ? errors.phoneNumber : ' '}
          inputProps={{ inputMode: 'numeric', maxLength: 10 }}
        />
        <TextField
          required
          label="Email Address"
          type="email"
          value={value.emailAddress}
          onChange={(e) => update({ emailAddress: e.target.value })}
          error={hasError(errors.emailAddress)}
          helperText={hasError(errors.emailAddress) ? errors.emailAddress : ' '}
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
      {showErrors && errors.termsAccepted && (
        <Typography variant="caption" color="error" display="block" sx={{ mt: 0.5 }}>
          {errors.termsAccepted}
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
