# Contribution workflow

Both repositories use `main` as the default branch. Once Jacob finishes reviewing and authorizes the initial push, place the initial scaffold commits on `main`. Subsequent feature changes go through pull requests targeting `main`. Do not commit or push while the initial local review is still in progress.

Make each set of feature changes on a feature branch and open a GitHub pull request for Jacob to review before merging. Do not push feature changes directly to the default branch or merge without his approval. Include the behavior changed and relevant validation in each PR.

Keep UI and API changes in their respective repositories. When a feature spans both, link the companion PR and describe any deployment ordering.

# C# code style

Separate groups of `Assert` statements, awaited assertions, and assertion helpers such as `AssertProblem` from surrounding setup and actions with a blank line. Keep consecutive assertions together without requiring blank lines between them. Do not add padding against enclosing braces. Enforce this convention through code review for now; no custom analyzer is required.

Remove unused `using` directives and leave a blank line after the import group. Always use braces for control-flow bodies, including single-line conditions and loops. Separate blocks such as `if`, `foreach`, and `switch` from neighboring statements with a blank line before and after. Do not add padding against an enclosing opening or closing brace, and keep related `else`, `catch`, and `finally` clauses attached.

The repository `.editorconfig` configures unused-import checks, required braces, and blank lines after blocks for `dotnet format` and the existing CI format check. Rider/ReSharper settings additionally enforce blank lines before blocks when formatting; that before-block convention also needs code review because `dotnet format` does not support that setting.

Leave a blank line before a `return` statement when another statement precedes it in the same block. A return that is the first statement in a block does not need padding against the opening brace. UI ESLint enforces this convention; API Rider/ReSharper formatting supports it through the control-transfer spacing setting (which also applies to other control-transfer statements). The API CLI formatter does not enforce this specific rule.
