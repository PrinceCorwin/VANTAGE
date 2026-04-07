# Intro Video — Plan

**Audience:** Brand-new users (no prior VANTAGE experience)
**Format:** OBS screencast with voiceover
**Length:** Determined by content
**Goal:** Get a new user from "I just received the installer" to "I know what every part of the main window does and where to find things." Module-specific workflows are covered in their own videos.

---

## Section Outline

### 1. Welcome & What VANTAGE Is
- One-line description: WPF app for tracking construction progress (welding, bolt-ups, steel erection) on industrial projects for Summit Industrial.
- What this video covers: install, first launch, auto-updates, plugins, and a tour of every menu and area in the main window. **Not covered here:** how to actually use Progress, Schedule, etc. — those have their own videos.

### 2. Installing the App
- **On-screen:** show the current installer location so viewers see where it lives today
- **Voiceover:** "Navigate to the location in the email I sent you and double-click the installer file." (Don't hard-code a path in the script — it may move; the email is the source of truth.)
- **Windows SmartScreen warning:** "Windows protected your PC" → click **More info** → click **Run anyway**. Explain this is normal for internal apps and is safe.
- Walk through the installer dialog (any options/destination)
- App launches / desktop shortcut created

### 3. First Launch & Login
- Login screen
- **Important:** Users must be added to the Users table by an admin before they can log in. There are no self-service credentials.
- **If you see a login error or "user not found" warning:** stop, call Steve, and he'll add you to the system. Then try again.
- Once logged in: initial sync runs (pulling project data from Azure), and you land on the default view (Progress).

### 4. Tour of the Main Window
Quick orientation of the three main regions before diving in:
- **Top toolbar** (menus left, nav center, window controls right)
- **Content area** (whichever module is active)
- **Status bar** at the bottom

### 5. Top Toolbar — Left Side (App Menus)
Walk through each menu, briefly listing what's inside. Don't demo the actions — just orient the user.
- **File** — import/export Activities, P6 import/export, AI Takeoff import, Export Logs, Manage My Snapshots, Legacy Export
- **Tools** — Help Sidebar, Prorate MHs, Schedule Change Log, Schedule UDF Mapping, ROC Manager. *Mention plugins add their menu items here.*
- **Admin** — *(only visible to admins)* Brief mention only: "If you're an admin, you'll also see an Admin menu here for managing users, projects, snapshots, and rates. We'll cover that in its own video." Don't open it.

### 6. Top Toolbar — Center (Module Navigation)
The six nav buttons and a one-line "what it's for" each:
- **PROGRESS** — track activity completion and earned values
- **SCHEDULE** — view and edit P6 schedule data
- **PROG BOOKS** — generate printable progress books
- **WORK PKGS** — generate PDF work packages
- **ANALYSIS** — aggregated metrics and dashboards
- **TAKEOFFS** — AI-powered piping takeoff extraction (covered in its own video)

Click each one quickly so the viewer sees what each looks like, then return to Progress. **Demo against Project 99.999 (Sandbox)** for the entire video.

### 7. Top Toolbar — Right Side (Settings Menu — `⋮`)
Open the three-dot menu and walk through each item:
- **Help Sidebar (F1)** — searchable help, F1 anywhere
- **Feedback Board** — submit ideas / report bugs (encourage users to use this)
- **Import / Export Settings** — move settings between machines
- **Grid Layouts** — save/apply named column layouts
- **Plugin Manager** — *covered in next section*
- **Theme** — Dark, Light, Orchid, Dark Forest. Demo a quick theme switch.
- **See Release Notes** — view what's new in each release. Open it to show recent entries.
- **About VANTAGE: Milestone** — version info

Then the **Minimize / Maximize / Close** window buttons.

### 8. Auto-Updates
- App checks for updates on startup automatically
- When an update is available, the user is prompted — one click installs and restarts
- **Users never have to manually download or install updates** after the initial install
- Tie this back to "See Release Notes" so users know where to read what changed

### 9. Plugins
- **Concept:** Plugins are small add-ons that extend VANTAGE for specific project needs (e.g., importing a vendor's shipping report, processing a specific report format). They appear as menu items under the **Tools** menu once installed.
- **Created on demand:** Plugins are built per project for whoever needs them — if your project needs something custom, ask and we'll build it.
- **Plugin Manager** (`⋮` → Plugin Manager): show the dialog, the **Installed** and **Available** tabs, what install/update looks like.
- Auto-update: installed plugins update themselves at startup, just like the app.
- Don't install one live — just show the dialog and explain it.

### 10. Status Bar
The thin bar at the bottom of the window:
- **App version** (left) — also shown for support purposes
- **Current user** — who you're logged in as
- **Last Sync time** (right) — when you last pushed/pulled changes from Azure

### 11. Help Sidebar (F1)
- Press **F1** anywhere in the app to open the help sidebar
- Show it opening, briefly demo expanding/collapsing it, scrolling, the table of contents
- Encourage: "Any time you're stuck or want to know what something does, hit F1."

### 12. Wrap-Up
- Recap: you've installed the app, logged in, and you know what every menu and button does
- Next steps: watch the module-specific videos (Progress, Schedule, Work Pkgs, Prog Books, Analysis) to learn how to actually do your daily work
- Reminder: F1 for help, Feedback Board to report issues or request features

---

## Decisions (resolved)
1. **TAKEOFFS** — own video (`Plans/Tutorials/Takeoffs/`)
2. **Admin menu** — brief mention only in Intro; full coverage in its own video (`Plans/Tutorials/Admin/`)
3. **Installer location** — script says "navigate to the location in the email"; on-screen show current location for visual context
4. **Login credentials** — no self-service. Users added by admin. If login fails, viewer is told to call Steve.
5. **Demo data** — Project **99.999 (Sandbox)** throughout
