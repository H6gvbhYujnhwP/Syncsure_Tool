# SyncSure V9 Backend

## Overview

This is the SyncSure V9 backend implementation featuring a **single-license, quantity-based** pricing system. This represents a major architectural upgrade from the previous multi-license system.

## V9 Key Features

- **Single License Per Account**: Each customer account has exactly one license
- **Quantity-Based Pricing**: Pricing is based on device quantity, not license count
- **Stripe Integration**: Full V9 Stripe webhook and subscription management
- **Self-Healing Dashboard**: Automatic data synchronization and error recovery
- **Comprehensive Audit Logging**: Full request/response logging for debugging

## Architecture Changes

### Database Schema
- **accounts**: Customer account information
- **licenses**: Single license per account with `device_count` (no more `max_devices`)
- **subscriptions**: Stripe subscription management with quantity-based pricing
- **builds**: Agent builds linked to licenses
- **device_bindings**: Device registration and heartbeat data

### API Endpoints

#### V9 Endpoints (Primary)
- `GET /api/v9/dashboard/summary` - Complete dashboard data
- `POST /api/v9/stripe/sync-customer` - Stripe customer sync
- `POST /api/v9/stripe/webhook` - Stripe webhook handler

#### Legacy Endpoints (Compatibility)
- `GET /api/builds/customer/:email` - Agent builds (updated for V9)
- `POST /api/agent/bind` - Device binding
- `POST /api/agent/heartbeat` - Device heartbeat

## Deployment

### Environment Variables

Copy `.env.example` to `.env` and configure:

```bash
cp .env.example .env
# Edit .env with your actual values
```

### Render Deployment

This backend is designed for Render.com deployment:

1. **Connect Repository**: Connect this GitHub repository to Render
2. **Set Build Command**: `npm install`
3. **Set Start Command**: `npm start`
4. **Configure Environment Variables**: Set all variables from `.env.example`

### Local Development

```bash
npm install
npm start
```

## V9 Implementation Details

### Single License System
- Each account has exactly one license in the `licenses` table
- The `device_count` field represents the purchased quantity
- The `bound_count` field tracks actual connected devices
- Pricing is calculated as `device_count * price_per_device`

### Stripe Integration
- Subscriptions are quantity-based with `device_quantity` field
- Webhook handlers automatically sync subscription changes
- Self-healing mechanisms recover from sync failures

### Dashboard Integration
- V9 dashboard endpoint provides complete account summary
- Includes account, license, subscription, and summary data
- Eliminates need for multiple API calls from frontend

## Testing

Run the integration test suite:

```bash
node test-v9-locally.js
```

Expected result: All 7/7 tests should pass.

## Migration from Previous Version

The V9 system maintains backward compatibility with existing agents while implementing the new single-license architecture. Existing device bindings and heartbeat data are preserved.

## Version Information

- **Version**: 9.0.0
- **System**: Single License, Quantity-Based
- **Database**: PostgreSQL with V9 schema
- **Node.js**: 18+ required
- **Deployment**: Render.com optimized

---

**Implementation Date**: September 11, 2025
**Status**: Production Ready

