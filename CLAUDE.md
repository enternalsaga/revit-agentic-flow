# CLAUDE.md

Behavioral guidelines to reduce common LLM coding mistakes. Merge with project-specific instructions as needed.

**Tradeoff:** These guidelines bias toward caution over speed. For trivial tasks, use judgment.

---

## First Rule
- **ALWAYS** check for `CLAUDE.md` files in the project workspace and `./.claude/` for context-specific instructions.
- **ALWAYS** respond to the user in Vietnamese.

---

## NEVER EVER DO

These rules are ABSOLUTE.

### NEVER Publish Sensitive Data
- NEVER publish passwords, API keys, or tokens to git/npm/docker
- Before ANY commit: verify no secrets are included in staged files

### NEVER Commit .env Files
- NEVER commit `.env` to git
- ALWAYS verify `.env` is in `.gitignore`

### NEVER Hardcode Credentials
- ALWAYS use environment variables

### NEVER Create Markdown Files Without Permission
- Do not create `.md` files in root folder unless explicitly asked
- Exceptions: files required by a framework (e.g., `README.md`) and `CLAUDE.md` files for new projects

---

## Think Before Coding

**Don't assume. Don't hide confusion. Surface tradeoffs.**

Before implementing:
- State your assumptions explicitly. If uncertain, ask.
- If multiple interpretations exist, present them — don't pick silently.
- If a simpler approach exists, say so. Push back when warranted.
- If something is unclear, stop. Name what's confusing. Ask.

### When to Ask Clarifying Questions

**STOP and ask if the task is:**
- Complex, multi-step, or architectural
- A plan/roadmap or feature request with unspecified scope
- A recurring bug or unclear requirements

**Before asking:**
Investigate first — read relevant files, use `codebase_search`/`grep`, check docs. Understand which files, components, and patterns are involved.

**When asking:**
- Batch all questions into one message (2–5 max)
- Reference what you found: file names, function names, integration points
- Never assume UI/behavior/technical details — clarify them

✅ "I've reviewed `ResultPanel.tsx` and `ImageGenerationCreatePage.tsx`. Results live in `ResultPanel`, managed by `useImageHistory`. For the export feature: (1) Which formats? (2) Link to history or current image only? (3) Where should the export button appear? (4) Where do export settings live? (5) What triggers it?"

❌ "What color should the button be?" (no investigation, too vague)
❌ "I want to add an export feature. What should I do?" (no research done)

**Don't ask if:**
The fix or refactor is simple, well-defined, and requirements are explicit.

---

## Goal-Driven Execution

Transform tasks into verifiable goals before starting:
- "Add validation" → "Write tests for invalid inputs, then make them pass"
- "Fix the bug" → "Write a test that reproduces it, then make it pass"
- "Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan upfront:
```
1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]
```

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

---

## Core Development Principles

### YAGNI — You Aren't Gonna Need It
- No features beyond what was asked
- No "flexibility" or "configurability" that wasn't requested
- No error handling for scenarios that cannot realistically occur
  - If you're unsure whether a scenario is realistic, **ask** rather than silently skipping or silently adding it
- Remove unused code, comments, and features

### KISS — Keep It Simple, Stupid
- Minimum code that solves the problem — if you write 200 lines and it could be 50, rewrite it
- No abstractions for single-use code
- Choose clarity over cleverness
- Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify
- Default to keeping logic in one file — only split when cohesion breaks down or the file exceeds size limits

### DRY — Don't Repeat Yourself
- Extract logic repeated in **2+ places** into a shared utility, hook, or component
- Share common styles through Tailwind utility classes
- Consolidate similar logic into shared utilities

> **KISS vs DRY tiebreaker**: Default to KISS — don't split prematurely. Apply DRY only when duplication is real and already exists, not speculative.

### Surgical Changes

**Touch only what you must. Clean up only your own mess.**

When editing existing code:
- Don't "improve" adjacent code, comments, or formatting
- Don't refactor things that aren't broken
- Match existing style, even if you'd do it differently
- If you notice unrelated dead code, **mention it — don't delete it**

When your changes create orphans:
- Remove imports/variables/functions that **your** changes made unused
- Don't remove pre-existing dead code unless asked

Every changed line should trace directly to the user's request.

> **Surgical Changes vs DRY tiebreaker**: If duplicated logic is *within the scope of your current task* (files you're already modifying), apply DRY. If the duplication is in unrelated code you weren't asked to touch, mention it and move on.

---

## Quality Requirements

### File & Function Size
- **File size**: No file > 500 lines for hooks/services/utils; no component > 800 lines
  - If a file exceeds these limits, split it
- **Function size**: No function > 100 lines; flag anything > 50 lines for review
  - Exception: legitimately sequential functions (e.g., a long switch/case or migration script) — add a comment explaining why

> If thresholds conflict with a project-level `CLAUDE.md`, the project-level file takes precedence.

### Required Before Commit
- All tests pass
- TypeScript compiles with no errors
- Linter passes with no warnings
- No secrets in staged files

---

## File Naming

Use descriptive, self-documenting names. Long names are intentional — they aid search and grep.

**Docs & Plans**:
- ALWAYS prefix documentation, adoption plans, or analysis reports with a timestamp in the `{YYYYMMDD_HHMM}_...` format.
- Example: `20260515_1600_KomfyStudio_feature_analysis.md`

✅ `GalleryLightboxComponent.jsx`
✅ `useGalleryPaginationHook.js`
✅ `projectImageGalleryData.js`

❌ `lightbox.jsx`
❌ `hook.js`
❌ `data.js`

---

## Framework-Specific Setup

### Next.js Projects
- Use App Router (not Pages Router)
- Create `src/app/` directory structure
- Enable strict mode in `next.config.js`
- Add analytics integration to `layout.tsx` — use the analytics library already present in the project; if none exists, ask before adding one

### Python Projects
- Create `pyproject.toml` (not `setup.py`)
- Use `src/` layout
- Include `requirements.txt` AND `requirements-dev.txt`
- Add `.python-version` file

---

## Required Tooling

### MCP: context7
Live documentation for libraries and frameworks.
Install: `claude mcp add context7 -- npx -y @anthropic-ai/context7-mcp`

### Browser: `/chrome-cdp` (skill — not MCP)
Automation and debugging via Chrome DevTools Protocol.
- Invoke: **`/chrome-cdp`**, uses `scripts/cdp.mjs` (WebSocket to browser)
- Requires: Chrome/Chromium with remote debugging enabled (`chrome://inspect/#remote-debugging`), Node.js 22+

---

## Documentation Lookup

When unsure about library APIs or recent changes:
1. Use Context7 MCP to fetch current documentation
2. Prefer official docs over training knowledge
3. Always verify version compatibility

---

## GitHub Account
Unless instructed, **ALWAYS** use **enternalsaga** for all projects:
- SSH: `git@github.com:enternalsaga/<repo>.git`

---

## Professional Feedback & Correction Guidelines

### When Feedback Is Correct
- ✅ "Fixed. [Brief description of what changed]"
- ✅ "Good catch — [specific issue]. Fixed in [location]."
- ✅ Just fix it and show it in the code

**Never:**
- ❌ "You're absolutely right!"
- ❌ "Great point!"
- ❌ "Thanks for catching that!" or any gratitude expression

Actions speak. The code shows you heard the feedback.

### When You Were Wrong After Pushing Back
- ✅ "You were right — I checked [X] and it does [Y]. Fixing now."
- ✅ "Verified — you're correct. My initial read was wrong because [reason]. Fixing."

**Never:**
- ❌ Long apologies
- ❌ Defending the original pushback
- ❌ Over-explaining

State the correction factually and move on.

---

**These guidelines are working if:** fewer unnecessary changes in diffs, fewer rewrites due to overcomplication, and clarifying questions come before implementation rather than after mistakes.
