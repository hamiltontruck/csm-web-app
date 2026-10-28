# CSM Dashboard and Menu Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Apply the approved executive dashboard design to Blazor dashboard and navigation.
**Architecture:** Keep existing services, bindings and permission checks; change presentation in existing Razor components and isolated CSS.
**Tech Stack:** .NET 10 Blazor InteractiveServer, CSS.
**Spec:** docs/superpowers/specs/2026-10-29-dashboard-menu-design.md

## Global Constraints
- No backend, authentication, database or migration changes.
- Preserve all permission-controlled navigation and real dashboard metrics.
- Support 320, 390, 768 and 1440 pixel widths.

## Review Focus
- Long metric labels and currency values must wrap without horizontal overflow.
- Mobile menu must expose sign out and scroll within the viewport.
- Menu must close after route selection and respond to keyboard focus.
- Company selection and loading/error states must remain usable.
- CSS isolation must style NavLink child anchors and avoid global page regressions.

### Task 1: Dashboard presentation
- [ ] Update Dashboard.razor heading and metric order to match reference hierarchy.
- [ ] Replace Dashboard.razor.css with consolidated navy/gold responsive styles.
- [ ] Preserve code block, service calls and metric values; inspect data bindings against baseline.

### Task 2: Navigation and shell
- [ ] Replace checkbox/DOM click behavior with accessible Blazor toggle and closing handler.
- [ ] Preserve all routes and permission conditions; add visible self-contained SVG icons.
- [ ] Replace NavMenu and MainLayout CSS; remove template About bar.
- [ ] Verify mobile open/close/scroll/focus, desktop sticky navigation and tablet layout.

### Task 3: Verification and delivery
- [ ] Run .NET build if SDK is available; otherwise record exact limitation and rely on PR CI.
- [ ] Render a static markup fixture using the production CSS at target widths; clearly distinguish this from authenticated app QA.
- [ ] Review diff and create a PR with validation and remaining live QA stated accurately.
