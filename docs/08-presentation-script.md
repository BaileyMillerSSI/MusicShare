# Presentation Script — MusicShare Team Talk

> **Format:** ~20-30 min talk. Casual, conversational. Pair with slides that show screenshots/diagrams.
> Each section maps to a slide or small group of slides.

---

## Slide 1 — The Pitch (1 min)

> "So, I built a thing. It's called MusicShare.
>
> You know how you're in a group chat and someone shares a Spotify link, but half the people use Apple Music and the other half use YouTube Music, and nobody can listen to it?
>
> This fixes that.
>
> You paste in any music link — Spotify, YouTube, Apple Music — and it gives you the same song on all three platforms.
>
> And here's the twist: I built almost the entire thing using AI.
>
> A while back I decided to grab the $20/month Claude Pro subscription — mostly just to see what all the hype was about. I figured worst case I'd cancel it after a month. That subscription comes with access to Claude Code, which is Anthropic's coding agent. I picked a project idea I'd been sitting on for a while, and just... started throwing work at it to see how far it could go.
>
> Spoiler: pretty far."

---

## Slide 2 — What It Does (2 min)

Show a demo or screenshot of the app.

> "Here's how it works in practice.
>
> I open the app — it's installed on my phone as a PWA, so it's on my home screen like a native app. I can even 'Share to MusicShare' from Spotify directly, same as sharing to iMessage.
>
> I paste or share a Spotify URL. Two seconds later I've got links to YouTube Music and Apple Music for the same song.
>
> Under the hood, there's actually a pretty interesting distributed system making that happen — we'll get to that."

---

## Slide 3 — How It Was Built (2 min)

> "Here's the part I wanted to share with the team.
>
> I used Claude Code for the vast majority of this. Not Copilot — an AI agent. The difference is scope. Copilot helps you finish a line. Claude Code takes a GitHub issue and comes back with a pull request.
>
> The project went from zero to production deploy in about 4 days. Full feature set took about 7 weeks. And most of that 7 weeks was me thinking about what to build, not me typing code."

Show the git timeline slide.

> "You can see it in the commit history. Day one: baseline working app. By the end of week one: Spotify, YouTube, async messaging, parallel resolution — all in place. The AI moved fast because once the first pattern was established, every subsequent feature followed the same pattern."

---

## Slide 4 — The Architecture (3-4 min)

Show the data flow diagram.

> "Let me walk through what actually happens when you submit a URL.
>
> You hit POST /api/share. The API validates the URL, figures out which service it came from, creates a record in MongoDB, and then publishes an event to RabbitMQ.
>
> That event kicks off a saga — a state machine that orchestrates the whole resolution workflow. It first extracts metadata from the source (title, artist, album, duration, artwork) via the source service's API. Then it fans out in parallel: it sends resolve commands to all the other services at the same time.
>
> Each service has a consumer — a MassTransit message handler — that searches for the song on its platform. Results come back asynchronously. The saga tracks when all of them are done, then marks the share as complete.
>
> Meanwhile, the frontend is polling every second. When it sees 'Completed', it shows the result page with all the links.
>
> Total time: typically 1-3 seconds."

---

## Slide 5 — Cool C# Patterns (5 min)

> "Let me highlight a few of the patterns that made this work well — some of which you might not use day to day."

**Saga Pattern:**
> "The saga was probably the most interesting architectural decision. Instead of one big function that calls Spotify, then YouTube, then Apple in sequence — which would be slow and fragile — we have a state machine that coordinates async messages.
>
> The state machine persists its state in MongoDB. If the server crashes mid-resolution, the saga picks up where it left off when it restarts. And because the three service lookups run in parallel, the total time is bounded by the slowest one, not the sum of all three."

**CQRS with MediatR:**
> "Every API operation is a separate command or query class — all in one file. SubmitShare has its Request, its Handler, and its Response. GetShareResult has its Query, Handler, and Result. The controller is almost empty — it just calls `_mediator.Send()` and maps the result.
>
> This is a pattern you might know from vertical slice architecture. It makes each feature completely self-contained and easy to find."

**Decorator Pattern:**
> "This one's my favorite. When you search YouTube Music for a song, you might get 10 results. How do you know which one is right?
>
> We have a ConfidenceAdapter that wraps any music service adapter. It scores each result on title match, artist match, album match, and duration — weighted at 40/25/25/10. Results below the threshold get filtered out. The highest scoring one wins.
>
> The music service adapters have no idea this is happening. The consumers have no idea either. It's transparent — classic decorator."

---

## Slide 6 — .NET Aspire (3 min)

> "This project was also my first real use of .NET Aspire, and I'm a convert.
>
> One command — `dotnet run --project MusicShare.AppHost` — starts everything. MongoDB, RabbitMQ with the management UI, the .NET API, the Next.js frontend, and the Aspire Dashboard for traces and logs. No Docker Compose file. No 'remember to start MongoDB before the API' in the README.
>
> The really interesting part is that the same AppHost configuration that runs local dev also drives the Azure deployment. When you run `azd provision`, Aspire generates the Bicep from your AppHost.cs and deploys Azure Container Apps, the container registry, networking — the whole thing.
>
> No Terraform. No hand-written Bicep. Infrastructure defined in C#."

Show the Aspire Dashboard screenshot if you have one.

> "The dashboard is also genuinely useful. You get distributed traces out of the box — so when I'm debugging why a saga didn't complete, I can see the full message flow as a waterfall trace."

---

## Slide 7 — CI/CD (2 min)

> "The pipeline is pretty standard GitHub Actions: lint, build, test, then deploy.
>
> But there are two things worth calling out.
>
> First, the deploy uses `azd` — Azure Developer CLI — which reads the Aspire AppHost and handles the actual Azure deployment. So the same pattern: infrastructure as C#, not YAML or Bicep.
>
> Second, after every deployment, the pipeline warms the ISR cache. Next.js has this feature called Incremental Static Regeneration — share result pages get cached and served at CDN speed. After a deploy, the cache is cold. So the pipeline immediately calls an endpoint that re-renders every existing share page. By the time the first user opens a link after a deployment, it's already cached.
>
> Push to main → tests → deploy → cache warm → done. No manual steps."

---

## Slide 8 — The AI Workflow (4 min)

> "So how did this actually work day to day?
>
> Claude Code is a CLI tool. You run it in your terminal. It has access to your whole repo — it can read files, write files, run commands, run tests, make git commits, open GitHub PRs.
>
> I set up a CLAUDE.md file — basically a system prompt for the AI working in this repo. It documents the architecture, the conventions, the key files, the patterns to follow, even which agent to use for which type of work.
>
> For most features, my workflow was: create a GitHub issue, then tell Claude Code 'work on issue #37.' It would read the issue from GitHub, plan the implementation, write the code following the conventions in CLAUDE.md, run the tests, fix anything that broke, and open a PR."

Show a PR or commit that was AI-generated.

> "The `feat: confidence scoring (#37)` PR — that was basically end-to-end from the AI. I reviewed it, made a few comments, it iterated, and we merged it.
>
> I also set up specialized agents. There's a `react-component-expert` agent for frontend work, an `infra-devops-owner` agent for infrastructure changes, a `project-coordinator` agent that reads GitHub issues and delegates to the right specialist. When I'm working on a backend feature, the frontend agent isn't touching anything.
>
> The AI also used MCP tools that connect to the Roslyn compiler — so instead of grepping for class names, it was doing semantic symbol search. It navigated the codebase the same way you would in Visual Studio."

---

## Slide 9 — What AI Is Good At / What It Isn't (2 min)

> "I want to be honest about where AI helped and where it didn't.
>
> AI was great at: implementing patterns it's seen before — CQRS, Repository, Decorator — correctly the first time. Writing boilerplate that follows the existing structure. Writing tests in the right format. Keeping documentation accurate because it was using it.
>
> Where I still had to think: architecture decisions. Consolidating the Worker into the API — that was a judgment call about operational complexity. Tuning the confidence thresholds — the AI set YouTube's threshold too high, and it took real usage data to realize that. Deciding what to build and what not to build.
>
> The AI is a very fast, very consistent implementer. The engineering judgment still lives with you."

---

## Slide 10 — Takeaways (1-2 min)

> "So what's the takeaway?
>
> First: .NET Aspire is worth trying if you have a multi-service setup. The 'one command to run everything' experience is legitimately great for onboarding and local dev parity.
>
> Second: Claude Code is different from Copilot in a meaningful way. If you want to experiment with it, the entry point is CLAUDE.md — document your conventions and architecture, and the AI will follow them consistently.
>
> Third: AI-assisted development isn't magic. It's fast when the problem is well-defined. It still needs you for the fuzzy stuff — architecture, product decisions, debugging weird production behavior.
>
> And lastly: this is a PWA that you can install on your phone right now, so if you use multiple music streaming services — give it a try."

---

## Appendix — Likely Q&A

**"Why not just use a third-party service like Songlink/Odesli?"**
> There are existing tools for this, yeah. This was a passion project to learn — Aspire, MassTransit sagas, confidence scoring, PWA — not to compete with production services.

**"How accurate is the song matching?"**
> Pretty good for mainstream songs. The confidence scoring filters out obvious mismatches. The YouTube threshold had to be tuned up because YouTube results are noisier than Spotify. Edge cases — live versions, remixes, regional releases — can still trip it up.

**"Can I run this locally?"**
> `dotnet run --project MusicShare.AppHost` — you'll need MongoDB and RabbitMQ (Aspire handles both). You'll also need Spotify API credentials. CLAUDE.md has the full setup guide.

**"Is Apple Music actually supported?"**
> Partially — the adapter is a mock because Apple doesn't have a public search API. The infrastructure is there for when they open one up.

**"How much did Azure cost?"**
> Minimal — scale to zero is configured, so it costs nothing when idle. A few dollars a month for actual usage.
