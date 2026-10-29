# CSM dashboard and navigation

Implement the user-approved MagicPath executive dashboard (component 463441336603918336, revision 465641372741955584) in the existing Blazor application. Use the navy sidebar, gold rules, restrained Executive Overview heading, compact white metric cards and two content panels. Adapt to phones using a compact header and collapsible, independently scrolling menu.

Preserve all current permission-controlled routes, company selection, real dashboard data, loading and error states. Reference KPI names are placeholders; retain the existing verified project, employee and delay metrics and budget/operations panels. No backend, authentication, database or migration changes.

Approach: update existing Razor markup and replace accumulated CSS overrides with one responsive stylesheet per component. Rebuilding the frontend or replacing verified metrics with design placeholders would add unnecessary scope. The user approved the visual and explicitly requested its implementation.
