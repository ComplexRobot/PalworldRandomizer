# Changelog

## Unreleased — 3.0 development

### Maintenance

- Preserve existing configuration files and defaults while replacing exposed configuration fields with properties.
- Resolve documentation and code-style warnings without intentionally changing randomization or editor behavior.
- Update the transitive Microsoft.Bcl.Memory dependency to address its reported security advisory.
- Add regression checks for configuration compatibility, deep copies and the editor's virtualized spawn lists.

### Migration groundwork

- Separate randomizer settings from window controls while preserving existing defaults and legacy template values.
- Keep PalSchema exports tied to explicitly captured settings, including egg timers and damage caps.

The current interface remains Windows/WPF at this increment. Shared Avalonia hosts and UI follow in later increments.
