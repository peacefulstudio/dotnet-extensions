# dotnet-extensions

[![License](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)

Shared ASP.NET Core hosting, logging, and telemetry extensions for building
.NET services with sensible defaults.

## Status

Active. Curated public releases land here; ongoing development happens
on a separate working tree. Issues, discussions, and pull requests are
welcome on this repo.

## Packages

| Package | Description |
|---|---|
| `Peaceful.Extensions.Hosting` | ASP.NET Core hosting extensions: CORS, exception handling, health checks, Scalar/OpenAPI, and API versioning. |
| `Peaceful.Extensions.Serilog` | Serilog bootstrap logger, ASP.NET Core host wiring, and request-logging composition helpers. |
| `Peaceful.Extensions.Telemetry` | OpenTelemetry setup for tracing, metrics, and OTLP export. |

## Install

```bash
dotnet add package Peaceful.Extensions.Hosting
dotnet add package Peaceful.Extensions.Serilog
dotnet add package Peaceful.Extensions.Telemetry
```

## Build from source

You'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet test
```

## Contributing

Contributions are welcome from anyone in the .NET community. See
[CONTRIBUTING.md](CONTRIBUTING.md) for the dev setup, the red-green TDD
requirement, and the branch model. The per-PR checklist itself lives in
the PR template and is filled in when you open a PR. By participating you
agree to abide by the [Code of Conduct](CODE_OF_CONDUCT.md).

For security-sensitive bugs, please follow [SECURITY.md](SECURITY.md)
instead of opening a public issue.

## Project stewardship

`dotnet-extensions` is currently developed and maintained by **Peaceful
Studio OÜ** (Estonia, VAT EE102232996). The project is licensed under
Apache-2.0 with the explicit intent of community ownership: if and when
adoption warrants neutral governance, Peaceful Studio commits to
transferring this repository to a community-led organisation under the
same license terms. Contributions welcome from anywhere in the .NET
ecosystem; no CLA required.

## License

Apache-2.0. © 2026 Peaceful Studio OÜ. See [LICENSE](LICENSE) and
[NOTICE](NOTICE).
