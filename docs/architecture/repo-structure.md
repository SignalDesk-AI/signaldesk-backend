# Repo Structure

SignalDesk backend uses an Nx monorepo with deployable services under `apps/` and shared code/contracts under `libs/`.

```text
apps/
  gateway-bff/
  notification-service/
  search-service/
  ai-service/
  identity-service/
  workspace-service/
  support-service/
  knowledge-service/
  campaign-service/

libs/
  contracts/
    events/
    http/
    grpc/
  building-blocks/
    dotnet/
    nestjs/

infra/
docs/
scripts/
tests/
.github/workflows/
```

`libs/building-blocks/dotnet` and `libs/building-blocks/nestjs` are the canonical shared library paths. Do not create parallel shared-code roots such as `libs/dotnet/BuildingBlocks` or `libs/node/common`.

## .NET Service Layout

```text
apps/<service-name>/
  src/
    SignalDesk.<Context>.Domain/
    SignalDesk.<Context>.Application/
    SignalDesk.<Context>.Infrastructure/
    SignalDesk.<Context>.API/
  SignalDesk.<Context>.sln
```

## NestJS Service Layout

```text
apps/<service-name>/
  src/
    app/
      config/
      health/
      logging/
      messaging/
      <business-module>/
```
