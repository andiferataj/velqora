# Dependency patching and build-tool overrides
Angular stays on its supported 21.x line. Framework packages are pinned together at 21.2.25, CLI/build at 21.2.28, and Material/CDK at 21.2.14.

The Angular 21 build tool's transitive versions still included advisories after the framework/CLI patch update. Both package manifests therefore override esbuild to 0.28.1 and undici to 7.30.1. The normal production build and compiled-login browser smoke verify this combination. npm audit reports zero known advisories with the generated lockfiles.

Testcontainers 4.4.0 pulled an affected SSH.NET version. Updating the three Testcontainers modules to 4.16.0 resolves that restore-time audit failure; the test sources now use image-accepting builder constructors. The integration project compiles, while its Docker execution remains pending.

Keep direct frontend dependencies and overrides identical in the root and frontend manifests: local development installs at the root without workspace symlinks; the isolated frontend Docker context installs from its own manifest/lockfile. Use npm ci and preserve both lockfiles.
