# Changelog

## Unreleased — 3.0 development

### Maintenance

- Preserve existing configuration files and defaults while replacing exposed configuration fields with properties.
- Resolve documentation and code-style warnings without intentionally changing randomization or editor behavior.
- Update the transitive Microsoft.Bcl.Memory dependency to address its reported security advisory.
- Add regression checks for configuration compatibility, deep copies and the editor's virtualized spawn lists.

The existing Windows/WPF interface is unchanged in this maintenance pass. The Avalonia migration is not included yet.
