Translate all repository documentation from Spanish to English, and switch the repo to English-only from now on.

Scope: every Spanish text file in the repo, including but not limited to README.md, docs/GDD.md, docs/ai-log.md, docs/adr/*, docs/solver-results.md, docs/prompts/*, docs/release-notes-*.md, and any other .md file or Spanish comment outside .cs files. Search the whole repo; do not rely on this list.

Rules:

Translation only. Do not change, add, remove or reinterpret any content, decision or number. If something is ambiguous, translate it literally and list it in the report.
Keep structure exactly: headings, numbering, entry numbers and dates in the ai-log, version numbers, tables, code blocks, file paths and identifiers.
In-game UI strings quoted in the docs (for example “Tu turno”) stay in Spanish, because they are the game’s current text. Translating the game itself is a separate task.
Add a short note at the top of docs/ai-log.md and in README.md: the project was developed in Spanish, the documentation was translated to English on 2026-10-29, and the originals are preserved in git history.

Update CLAUDE.md conventions: all text written to the repository goes in English, including documentation, the ai-log, ADRs, prompts and commit messages. Conversation with Cami stays in Spanish.

GDD: add a version row for the translation.

Verification: search the repo for remaining Spanish (accented characters, ñ, ¿, ¡, and common Spanish words) outside quoted UI strings and .cs UI text, and report every remaining hit with its file and line.

Commit on its own branch, separate from any code change.