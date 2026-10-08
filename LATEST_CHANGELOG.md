## v3.1.0 (minor)

Changes since v3.0.0:

- Register AddNavigationProvider as transient so both DI setups pass scope validation [minor] ([@Claude](https://github.com/Claude))
- Keep the history when a restored state aliases the live Commands and SaveBoundaries [patch] ([@Claude](https://github.com/Claude))
- Move the placeholder round-trip tests next to the malformed-load test ([@Claude](https://github.com/Claude))
- Keep a placeholder's saved type, description and data through a save [patch] ([@Claude](https://github.com/Claude))
- Keep a reloaded command's navigation context and metadata [patch] ([@Claude](https://github.com/Claude))
- Move CI onto the shared ci-shared.yml pipeline ([@Claude](https://github.com/Claude))

