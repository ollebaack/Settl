# ADR-0003: Backend is ASP.NET Core Minimal API on .NET 10

- **Status:** accepted
- **Date:** 2026-07-12

## Context

The developer knows .NET well. Alternatives (full-TS backend, BaaS) would be faster to
wire types across the API boundary but slower for someone productive in C#.

## Decision

We will build the API with ASP.NET Core Minimal APIs on .NET 10 (LTS), with built-in
OpenAPI generation as the contract source (see ADR-0006).

## Consequences

Two languages and toolchains in one repo; the API boundary needs codegen (accepted, see
ADR-0006). In exchange: strong typing, EF Core migrations, first-class background-job
options for recurring expenses and reminders (hosted services), and developer velocity
where it counts.
