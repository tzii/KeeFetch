# Contributing to KeeFetch

Thank you for your interest in contributing to KeeFetch!

## How to Contribute

1. **Fork the Repository**: Create your own fork of the project.
2. **Clone the Fork**: `git clone https://github.com/YOUR_USERNAME/KeeFetch.git`
3. **Create a Branch**: `git checkout -b feature/your-feature-name`
4. **Make Changes**: Implement your feature or fix.
5. **Add Tests**: Ensure your changes are covered by unit tests in `KeeFetch.Tests`.
6. **Run Tests**: Use `dotnet test` to verify everything is working.
7. **Commit Changes**: `git commit -m 'feat: add amazing feature'`
8. **Push to GitHub**: `git push origin feature/your-feature-name`
9. **Open a Pull Request**: Submit your PR for review.

## Coding Standards

- Follow existing code style (see `.editorconfig`).
- Use XML documentation for public and internal members.
- Keep methods small and focused.
- Avoid external dependencies unless absolutely necessary.

## Development Environment

- Visual Studio 2022 or VS Code with C# Dev Kit.
- .NET 8 SDK (for building the project).
- .NET Framework 4.8 targeting pack.
- KeePass 2.x installed for testing and PLGX creation.

## Building the PLGX

The PLGX is built using the `KeeFetch.plgx.csproj` file which is a legacy-style project file required by KeePass. The main `KeeFetch.csproj` is an SDK-style project used for modern development and testing.
