# Contributing to LGPD.NET

Thank you for your interest in contributing! We welcome contributions from the community.

## Getting Started

1. Fork the repository
2. Clone your fork: `git clone https://github.com/your-username/lgpd-dotnet.git`
3. Create a branch: `git checkout -b feature/your-feature-name`
4. Build: `dotnet build`
5. Test: `dotnet test`

## Code Conventions

- Follow the existing code style (`.editorconfig` enforces this automatically)
- All public API must be in **English**
- Use nullable reference types
- Treat warnings as errors
- XML documentation on all public types and members
- Keep Core package dependency-free

## Pull Request Process

1. Ensure all tests pass: `dotnet test`
2. Add tests for new functionality
3. Update documentation if needed
4. Update `CHANGELOG.md` with your changes
5. Submit the PR with a clear description

## Commit Messages

Use conventional commits:

- `feat:` new feature
- `fix:` bug fix
- `docs:` documentation changes
- `test:` test additions or changes
- `refactor:` code refactoring
- `chore:` build, CI, or tooling changes

## License

By contributing, you agree that your contributions will be licensed under the MIT License.
