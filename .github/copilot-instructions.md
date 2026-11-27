## Dependency Management & Security

- Always use the latest stable and compatible versions of NuGet packages, frameworks, SDKs and dependencies.
- Verify current versions against official package registries before installation.
- Never introduce outdated, deprecated or known vulnerable dependencies.
- Check both direct and transitive dependencies for security vulnerabilities.
- Prefer the latest major release and adapt the implementation to its current APIs.
- Do not use preview, alpha, beta or release candidate versions unless explicitly requested.
- Never suppress security warnings to achieve a successful build.
- Maintain compatibility with .NET 11.
- Any exception to these requirements must be justified and approved before implementation.
- Build the solution and execute relevant tests after dependency updates.