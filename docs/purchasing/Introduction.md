# Purchasing

## Introduction

The Purchasing module provides a complete in-app purchase solution for Unity games. It handles item catalog management, payment processing through external gateways, purchase verification, and receipt validation. Key features include:

- **Dynamic Item Catalog**: Fetch and display purchasable items from the server
- **Secure Payment Processing**: Integration with payment gateways for safe transactions
- **Purchase Verification**: Automatic and manual verification of completed purchases
- **Receipt Management**: Handle payment confirmations and invoice generation
- **Event-Driven Architecture**: Comprehensive events for all purchase states
- **Offline Support**: Local caching and verification retry on app resume

## Features

- Real-time item availability updates
- Batch purchase verification for performance
- Automatic verification on app focus regain
- Support for multiple payment methods
- Analytics integration for purchase tracking
- Deeplink handling for payment completion

## Integration

See [Integration](Integration.md) for detailed setup and usage.

Demo scene: `Assets/FlyingAcorn/Soil/Purchasing/Demo/SoilPurchasingExample.unity`

## Dependencies

- Core SDK (for authentication and networking)
- Remote Config (for dynamic purchasing settings)