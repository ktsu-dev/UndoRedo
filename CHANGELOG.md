## v1.0.1-pre.1 (prerelease)

Changes since v1.0.0:

- Update project configuration and CI/CD settings: modify .editorconfig for variable declaration preferences, enhance .gitignore for SpecStory files, adjust .runsettings for test parallelization, update package versions in Directory.Packages.props, and refine GitHub Actions workflow for better release management and SonarQube integration. ([@matt-edmondson](https://github.com/matt-edmondson))
## v1.0.0 (major)

- Initial commit for UndoRedo ([@matt-edmondson](https://github.com/matt-edmondson))
- Enhance testing documentation in derived-cursor-rules.mdc with detailed commands for restoring dependencies, building, and running tests. Add new testing discussion document outlining test execution steps and troubleshooting for the UndoRedo project. ([@matt-edmondson](https://github.com/matt-edmondson))
- Add project configuration files and ignore rules; remove sample applications ([@matt-edmondson](https://github.com/matt-edmondson))
- Add project configuration files: Directory.Packages.props for centralized package version management and global.json for SDK and MSBuild SDK versions. Update UndoRedo.Core and UndoRedo.Test project files to remove specific versioning for dependencies and SDKs. ([@matt-edmondson](https://github.com/matt-edmondson))
- Add .cursorignore for SpecStory backup files, enhance README with detailed library overview, features, and usage examples; add architecture and best practices documentation; implement core undo/redo functionality with command management and dependency injection support. ([@matt-edmondson](https://github.com/matt-edmondson))
