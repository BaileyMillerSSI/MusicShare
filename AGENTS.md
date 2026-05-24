# Repository Guidelines

## Project Structure & Module Organization

MusicMatcher resolves shared music URLs across Spotify, Apple Music, and YouTube Music. `MusicShare.Api` hosts endpoints, CQRS commands/queries, consumers, and the share saga. `MusicShare.Services` contains business logic and provider adapters. `MusicShare.Persistence` contains MongoDB entities and repositories. `MusicShare.Contracts` defines shared enums and messages. `MusicShare.AppHost` is the Aspire entry point. Frontend code lives in `MusicShare.Frontend/src`: routes in `src/app`, components in `src/components`, hooks in `src/hooks`, and utilities in `src/lib`. Assets are in `MusicShare.Frontend/public`. Tests are in `MusicShare.Tests` and colocated frontend `*.test.ts(x)` files.

## Architecture Notes

The backend uses MediatR CQRS, MassTransit sagas, MongoDB repositories, and Aspire. The frontend is a Next.js App Router PWA with React Query, Tailwind CSS, and Web Share Target support.

## Build, Test, and Development Commands

- `dotnet build MusicShare.slnx`: compile all backend projects.
- `dotnet test MusicShare.Tests/MusicShare.Tests.csproj`: run backend xUnit tests.
- `dotnet run --project MusicShare.AppHost`: start Aspire, MongoDB, RabbitMQ, API, and frontend.
- `cd MusicShare.Frontend && npm run dev`: run the Next development server.
- `cd MusicShare.Frontend && npm run build`: build the frontend with Next.
- `cd MusicShare.Frontend && npm run lint`: run ESLint.
- `cd MusicShare.Frontend && npm test`: run Vitest once.

## Coding Style & Naming Conventions

C# uses nullable reference types and implicit usings. Use PascalCase for public types and methods, camelCase for locals and parameters, `I*` interfaces, file-scoped namespaces, and primary constructors for dependency injection. Keep API command/query files as static operation classes with nested request/query, handler, and response/result types. TypeScript uses ES modules, React function components, PascalCase component files, and camelCase hooks such as `usePWAInstall.ts`.

## Testing Guidelines

Backend tests use xUnit v3, FluentAssertions, Moq, Autofac.Extras.Moq, and Aspire integration testing. Name backend test files after the unit under test, for example `SongServiceTests.cs`, and prefix unit test methods with `ItWill`. Prefer per-test `AutoMock.GetLoose()` setup. Frontend tests use Vitest, Testing Library, and `happy-dom`; colocate tests as `Component.test.tsx` or `utils.test.ts`.

## Commit & Pull Request Guidelines

Recent history uses short, imperative subjects with Conventional Commit prefixes, such as `feat: add frontend proxy routes for song re-indexing`. Create issue branches as `feat/issue-<number>-<short-name>`. Keep commits focused and avoid unrelated refactors. PRs should target `main`, link issues with `Closes #<number>` when applicable, include test results, and add screenshots for UI/PWA changes.

## Security & Configuration Tips

Do not commit secrets. Use user secrets or local environment variables for provider credentials, connection strings, and revalidation tokens.
