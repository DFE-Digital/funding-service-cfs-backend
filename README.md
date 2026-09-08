# funding-service-cfs-backend


## Overview

This repository is part of the Department for Education (DfE) Calculate Funding Service (CFS).

It provides responsible for funding calculations, orchestration, and related APIs, supporting the calculation, allocation, and management of education funding.

## Key Features

- Funding calculation processing
- Integration with Calculate Funding Service components
- Secure and scalable application architecture
- Automated CI/CD deployment pipeline
- Monitoring and operational support

## Architecture

This service forms part of the wider Calculate Funding Service platform and integrates with:

- Calculate Funding APIs
- Azure services
- Data storage components
- Other CFS microservices

## Prerequisites

Before running the application locally, ensure you have:

- .NET SDK (required version)
- Azure access (where applicable)
- Docker (if applicable)
- Git

## Getting Started

Clone the repository:

```bash
git clone <repository-url>
cd unding-service-cfs-backend
```

Restore dependencies:

```bash
dotnet restore
```

Build the application:

```bash
dotnet build
```

Run locally:

```bash
dotnet run
```

## Configuration

Application configuration is managed through:

- `appsettings.json`
- Environment variables
- Azure App Configuration (where applicable)
- Azure Key Vault (where applicable)

## Testing

Run unit tests:

```bash
dotnet test
```

## Deployment

Deployments are performed through the configured CI/CD pipeline.

Production changes must follow the approved change management and release processes.

## Contributing

This repository is maintained by the Calculate Funding Service team.

Please:

- Follow DfE development standards
- Create pull requests for all changes
- Ensure tests pass before submission
- Obtain appropriate peer review

## Support

For support, contact the Calculate Funding Service team.

## CFS Wiki-
https://educationgovuk.sharepoint.com/sites/Calculatefundingserviceopenspace/SitePages/CFS---Calculate-Funding-Service.aspx

## License

Internal Department for Education repository. All rights reserved.
`
