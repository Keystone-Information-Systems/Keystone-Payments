# ValPay Frontend

A modern React + TypeScript frontend for the ValPay payment processing system. This application provides a secure, responsive interface for processing payments through Adyen integration.

## Features

- 🚀 **Modern Stack**: React 18 + TypeScript + Vite
- 💳 **Payment Processing**: Adyen integration for secure payments
- 🎨 **Material-UI**: Beautiful, responsive design system
- 🔄 **Real-time Updates**: Live transaction status monitoring
- 📱 **Mobile-First**: Fully responsive design
- 🛡️ **Security**: HTTPS-only, CSP headers, secure payment handling
- ⚡ **Performance**: Optimized builds with code splitting
- 🧪 **Testing**: Comprehensive test coverage
- 🌍 **Accessibility**: WCAG compliant components

## Tech Stack

- **Frontend**: React 18, TypeScript 5, Vite
- **UI Library**: Material-UI (MUI) 5
- **State Management**: Zustand
- **API Client**: Axios + React Query
- **Forms**: React Hook Form + Zod validation
- **Routing**: React Router v6
- **Payment**: Adyen Web SDK
- **Styling**: Emotion (CSS-in-JS)

## Project Structure

```
src/
├── components/          # Reusable UI components
│   ├── ui/             # Basic UI components
│   ├── payment/        # Payment-specific components
│   └── layout/         # Layout components
├── pages/              # Page components
├── hooks/              # Custom React hooks
├── services/           # API services
├── stores/             # Zustand state stores
├── types/              # TypeScript type definitions
├── utils/              # Utility functions
└── styles/             # Global styles and theme
```

## Getting Started

### Prerequisites

- Node.js 18+ 
- npm or yarn
- AWS CLI (for deployment)

### Installation

1. Clone the repository:
```bash
git clone <repository-url>
cd valpay-frontend
```

2. Install dependencies:
```bash
npm install
```

3. Create environment file:
```bash
cp env.example .env
```

4. Configure environment variables:
```env
VITE_API_BASE_URL=https://your-api-gateway-url
VITE_ADYEN_CLIENT_KEY=your-adyen-client-key
VITE_ENVIRONMENT=test
VITE_APP_NAME=ValPay
VITE_APP_VERSION=1.0.0
```

### Development

Start the development server:
```bash
npm run dev
```

The application will be available at `http://localhost:3000`.

### Building

Build for production:
```bash
npm run build
```

The built files will be in the `dist/` directory.

### Testing

Run tests:
```bash
npm run test
```

Run tests in watch mode:
```bash
npm run test:watch
```

## Payment Flow

1. **Payment Methods**: Fetch available payment methods from backend
2. **Method Selection**: User selects preferred payment method
3. **Payment Form**: Display Adyen payment component
4. **Payment Processing**: Submit payment to Adyen via backend
5. **Status Monitoring**: Real-time transaction status updates
6. **Result Display**: Show success/error page with transaction details

## API Integration

The frontend integrates with the ValPay backend through three main endpoints:

- `POST /paymentMethods` - Get available payment methods
- `POST /payments` - Create payment with Adyen
- `GET /transactions/{id}` - Retrieve transaction status

## Deployment

### AWS S3 + CloudFront

1. Build the application:
```bash
npm run build
```

2. Deploy to S3:
```bash
npm run deploy
```

3. Invalidate CloudFront cache:
```bash
npm run invalidate
```

### Environment Configuration

Set the following environment variables in your deployment:

- `VITE_API_BASE_URL`: Your API Gateway URL
- `VITE_ADYEN_CLIENT_KEY`: Your Adyen client key
- `VITE_ENVIRONMENT`: `test` or `live`

## Security

- **HTTPS Only**: All connections use HTTPS
- **CSP Headers**: Content Security Policy implemented
- **No Sensitive Data**: No payment data stored in localStorage
- **Adyen SDK**: Secure payment processing through Adyen
- **Input Validation**: All forms validated with Zod schemas

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Submit a pull request

## License

This project is proprietary software. All rights reserved.

## Support

For support and questions, please contact the development team.
