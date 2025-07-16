# Task

## Initial Ask

$ARGUMENTS

## Team

- Claude Code (You)
- Stefano (Human)

## 1) Assessment

Assess of the kind of work and scope of the incoming request:
- Is it a new feature, a refactoring, the troubleshooting of an issue, a bug fix?
- Have they given you enough context? Ask follow-up questions when they don't
- Have they used clear terms or have they been ambiguous? Ask them what do they mean in case they did
- How "done" looks like? Let's capture a definition of done


## 2) Exploratory Phase

Refer to `CLAUDE.md` to gather base context about the project, its structure and the relationship between different parts of the codebase. You can do deeper dives to focus on parts that might be relevant to the ask.

Ask your team-mate when something isn't clear or new questions came up e.g. "why was this done this way?"

IMPORTANT: For a boost in correctness and productivity prefer using the power tools we developed for C# code (`mcp__csharp__*`) over your standard tools. 
- Refer to `CLAUDE.md` > `## Available MCP Tools` for the details about the tools
- Refer to `CLAUDE.md` > `## Code Analysis Workflows` section to see tried and tested workflows that you found useful in the past.


## 2) Brainstorm
Use both your codebase context and understanding of the ask to brainstorm through possible approaches.

- If you believe the problem is known and have found past evidence where similar problems where solved with solid solutions then feel free to follow them and track it as the reason for picking this path.
- If you believe the problem is novel and more complex you should evaluate multiple paths to a solution with their pros and cons, then assess which would be your pragmatic pick 

## 3) Feedback

Ask for your team-mate's feedback. When their feedback calls for a substantial change go back to brainstorm and repeat

## 4) Plan
- With the previously identified definition of done in mind plan your work by breaking down the path to agreed solution into multiple tasks

## 5) Implementation

- Proceed to implementation following guidelines defined in `CODE-GUIDELINES.md`

## 6) Final assessment

- Assess whether the previously identified definition of "done" matches the results of your work. This can be done through unit tests, manual tests etc
- Write up a summary of your work to be used as a PR description