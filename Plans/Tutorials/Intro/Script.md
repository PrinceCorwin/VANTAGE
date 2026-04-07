# Intro Video — Script

**Format:** Two-column. LEFT = on-screen action / what to show in OBS. RIGHT = voiceover.
**Demo project:** 99.999 (Sandbox)

---

## 1. Welcome

| On Screen | Voiceover |
|---|---|
| Title card or VANTAGE logo / splash. | Welcome to VANTAGE: Milestone. This is the introduction video for new users. |
| Hold on logo / desktop. | VANTAGE: Milestone is the application Summit Industrial uses to track construction progress on industrial projects — things like welding, bolt-ups, and steel erection. |
| Hold. | In this video I'll show you how to install the app, log in for the first time, and give you a complete tour of the main window — every menu, every button, and what each part does. We're not going to dive into how to actually use the Progress module, the Schedule module, or any of the others — each of those has its own dedicated video. This one is just to get you oriented. |

---

## 2. Installing the App

| On Screen | Voiceover |
|---|---|
| Show File Explorer open at the current installer location (the share/folder where the installer file lives today). Highlight the installer file. | To install VANTAGE, navigate to the location I sent you in the email and double-click the installer file. |
| Double-click the installer. The blue Windows SmartScreen dialog appears: "Windows protected your PC". | When you double-click it, you may see this blue Windows warning that says "Windows protected your PC." This is normal — Windows shows this warning for any application that isn't published by a major commercial vendor. VANTAGE is safe; it's our internal app. |
| Click **More info**. | To get past it, click **More info**... |
| Click **Run anyway**. | ...and then click **Run anyway**. |
| Installer dialog appears. Click through Next / Install. Show the install progress bar. | The installer will walk you through a few simple steps. Just click through them — the defaults are fine. |
| Installer finishes. Show the new VANTAGE shortcut on the desktop. | When it's done, you'll have a VANTAGE shortcut on your desktop. |

---

## 3. First Launch & Login

| On Screen | Voiceover |
|---|---|
| Double-click the desktop shortcut. App splash screen appears, then login dialog. | Double-click the shortcut to launch VANTAGE for the first time. After a brief splash screen, you'll see the login window. |
| Type username, then password. | Enter your username and password and click Login. |
| **Important callout** — pause briefly here. | One important note: VANTAGE doesn't have self-service signup. Before you can log in, an administrator has to add you to the user list. |
| Show a hypothetical login error toast (or just describe). | So if you launch the app and you get a warning that says you're not authorized, or your login is rejected — don't worry, nothing's broken. Just give me a call and I'll add you to the system. Once I do, try logging in again and you'll be in. |
| Login succeeds. App shows a brief loading spinner — initial sync. Then lands on the Progress view. | Once you log in successfully, the app does an initial sync — that's pulling all your project data down from our central Azure database. Depending on how much data is on your projects, this may take a few seconds. |
| Progress view fully loaded with Project 99.999 data visible. | And here we are. This is the main VANTAGE window. |

---

## 4. Tour of the Main Window — Three Regions

| On Screen | Voiceover |
|---|---|
| Highlight the top toolbar (40px row at top) with a callout/box. | Before we go through every menu, I want to point out the three main regions of the window. At the top, you have the toolbar — this contains your menus, your module navigation, and your window controls. |
| Highlight the large content area in the middle. | In the middle is the content area. This is where the active module is displayed — right now we're looking at the Progress module, but this area changes when you click any of the navigation buttons. |
| Highlight the thin status bar at the bottom. | And at the bottom is the status bar. We'll come back to that at the end. |
| Remove highlights. | Let's start with the top toolbar, working from left to right. |

---

## 5. Top Toolbar — Left Side (App Menus)

### File Menu

| On Screen | Voiceover |
|---|---|
| Click the **File** menu. Hold it open. | On the left side of the toolbar you have your application menus. The first one is **File**. |
| Hover slowly down each section. | The File menu is where you import and export Activities — that's the main data the Progress module works with. You can import from a Milestone or Legacy Excel format, and you can export the same way. |
| Hover the P6 items. | You'll also find P6 import and export here — that's how you bring in your schedule data from Primavera. |
| Hover Manage My Snapshots, Export Logs. | And there are a few utility items down here: Manage My Snapshots, Export Logs, and a couple of legacy export options for anyone still working with the old system. |
| Hover "Import from AI Takeoff". | We won't dig into any of this in this video — every one of these features is covered in its module's own tutorial. I just want you to know where things live. |
| Close the File menu. | |

### Tools Menu

| On Screen | Voiceover |
|---|---|
| Click the **Tools** menu. Hold open. | Next to File is **Tools**. |
| Hover Help Sidebar. | Up at the top is the Help Sidebar — you can also open it any time with F1, and we'll talk about that in a minute. |
| Hover Prorate MHs, Schedule Change Log, Schedule UDF Mapping, ROC Manager. | Below that you have a few utilities: Prorate MHs, the Schedule Change Log, Schedule UDF Mapping, and ROC Manager. Again, each of these is covered in detail in the relevant module video. |
| Hover the bottom of the menu where plugin items would appear. | One thing to note: any plugins you have installed will add their menu items down here in the Tools menu. We'll talk about plugins in a few minutes. |
| Close the menu. | |

### Admin Menu (brief mention)

| On Screen | Voiceover |
|---|---|
| Hover near where the Admin menu would be. (If logged in as admin, briefly point to it but don't open. If not admin, gesture to the empty space.) | If you're an administrator, you'll also see an **Admin** menu here, with options for managing users, projects, snapshots, and rates. If you're a regular user, you won't see this menu at all — that's normal. There's a separate video that covers everything in the Admin menu. |

---

## 6. Top Toolbar — Center (Module Navigation)

| On Screen | Voiceover |
|---|---|
| Highlight the row of nav buttons in the center of the toolbar: PROGRESS / SCHEDULE / PROG BOOKS / WORK PKGS / ANALYSIS / TAKEOFFS. | In the center of the toolbar you have your module navigation. These six buttons are how you switch between the different parts of the application. Let me show you each one quickly. |
| Click **PROGRESS**. (Already on it — re-click to confirm.) | **PROGRESS** is where you'll spend most of your time. This is the grid where you track activity completion and earned values for each project. |
| Click **SCHEDULE**. Wait for it to load. | **SCHEDULE** is where you view and edit your P6 schedule data — the schedule rows and the activities linked to them. |
| Click **PROG BOOKS**. | **PROG BOOKS** is where you generate printable progress books — the PDF checklists you take out into the field. |
| Click **WORK PKGS**. | **WORK PKGS** generates PDF work packages with forms and drawings. |
| Click **ANALYSIS**. | **ANALYSIS** gives you aggregated metrics and dashboards across all your activities. |
| Click **TAKEOFFS**. | And **TAKEOFFS** is the AI-powered piping takeoff extraction module — that one has its own dedicated video because there's a lot to it. |
| Click **PROGRESS** to return. | Each of these has its own tutorial video where we go into detail. For now, let's head back to Progress. |

---

## 7. Top Toolbar — Right Side (Settings Menu — `⋮`)

| On Screen | Voiceover |
|---|---|
| Point to the three-dot button in the top right corner. Click it to open the popup. | On the far right of the toolbar you'll see a three-dot icon. This is the Settings menu. Let me walk you through what's in here. |
| Hover **Help Sidebar**. | At the top is **Help Sidebar** — same as in the Tools menu, opens the help panel. |
| Hover **Feedback Board**. | **Feedback Board** is where you can submit ideas for new features or report bugs you run into. I look at this regularly, so please use it — it's the best way to get something fixed or improved. |
| Hover **Import Settings** and **Export Settings**. | **Import Settings** and **Export Settings** let you save your application settings to a file and load them on another machine. Handy if you switch computers. |
| Hover **Grid Layouts**. | **Grid Layouts** is where you save and apply named column layouts for the grids — for example, you might have one layout for welding work and another for bolt-ups. We cover this in the Progress video. |
| Hover **Plugin Manager**. | **Plugin Manager** — we'll come back to this in a moment. |
| Hover **Theme**. Click it. The theme dialog/submenu opens. | **Theme** lets you change the look of the app. We have Dark, Light, Orchid, and Dark Forest themes built in. |
| Switch to Light theme. Pause. Switch to Orchid. Pause. Switch back to Dark (or whatever the default is). | You can switch live — no restart needed. Pick whichever one is easiest on your eyes. |
| Re-open the Settings menu. Hover **See Release Notes**. Click it. | **See Release Notes** opens up a window showing you what's new in each release of the app. |
| Show the release notes dialog briefly, scroll. Close it. | Whenever the app updates, you can pop in here to see exactly what changed. |
| Re-open Settings. Hover **About VANTAGE: Milestone**. | And **About VANTAGE: Milestone** just shows you the version number and some basic info about the app. |
| Close the menu. Point to the minimize / maximize / close buttons in the upper right. | And of course, on the far right of the toolbar are your standard Windows minimize, maximize, and close buttons. |

---

## 8. Auto-Updates

| On Screen | Voiceover |
|---|---|
| Hold on the main window. Optional: cut to a screenshot of the update prompt dialog if available. | Now let's talk about updates. One of the nicest things about VANTAGE is that you almost never have to think about updating it. |
| Hold. | Every time you launch the app, it automatically checks to see if a newer version is available. If there is one, you'll get a prompt asking if you want to install it. You click yes, the update installs, the app restarts, and you're on the new version. |
| Hold. | After this initial install you just did, you should never have to manually download or run an installer again. The app takes care of itself. |
| Open Settings → See Release Notes briefly to tie back. Close it. | And if you ever want to see what changed in a recent update, that's what the See Release Notes button is for. |

---

## 9. Plugins

| On Screen | Voiceover |
|---|---|
| Hold on main window. | Let's talk about plugins for a minute. |
| Hold. | A plugin is a small add-on that extends VANTAGE for a specific need. For example, one of our plugins reads a vendor shipping report from a particular fabricator and automatically updates the matching activities in VANTAGE. Another one does the same thing for a different vendor. |
| Hold. | Plugins are built on demand. If your project needs something custom — a specific report imported, a particular data format processed, anything like that — let me know and I'll build a plugin for it. They're created per project, for whoever needs them. |
| Hold. | Once a plugin is installed, it shows up as a menu item in the **Tools** menu. You just click it like any other feature. |
| Open Settings (`⋮`) → click **Plugin Manager**. | And if you want to see what plugins are available or what you have installed, that's what the Plugin Manager is for. Open the Settings menu and click Plugin Manager. |
| Plugin Manager dialog opens. Show the **Installed** tab. | Here on the **Installed** tab you can see what's already on your machine. |
| Click the **Available** tab. | And on the **Available** tab you can see what plugins exist that you haven't installed yet. Installing one is just a single click. |
| Hold. | Just like the app itself, your installed plugins update themselves automatically every time you launch VANTAGE. You never have to think about keeping them current. |
| Close the Plugin Manager dialog. | I'm not going to install one in this video — I just wanted you to know they exist and where to find them. |

---

## 10. Status Bar

| On Screen | Voiceover |
|---|---|
| Highlight the status bar at the bottom of the window. | Now down at the bottom of the window we have the status bar. It's small but it's useful. |
| Point to the version text on the left. | On the left is your **app version**. If you ever call me about an issue, I'll probably ask you what version you're on — this is where you'd look. |
| Point to the current user. | Next to that is **the user you're logged in as**. |
| Point to the Last Sync text on the right. | And on the right is **Last Sync** — the timestamp of the last time you pushed your changes up to Azure or pulled changes down. If it says "Never," you haven't synced yet this session. |

---

## 11. Help Sidebar (F1)

| On Screen | Voiceover |
|---|---|
| Press **F1**. The Help Sidebar slides open on the right side of the window. | The last thing I want to show you is the Help Sidebar. You can open it any time, from anywhere in the app, by pressing **F1**. |
| Scroll through the help table of contents. Click into a section like "Progress" or "Getting Started". | This is your built-in user manual. It's organized by module — Getting Started, Progress, Schedule, Work Packages, all of it. There's also a search box at the top if you're looking for something specific. |
| Show the splitter — drag the sidebar wider, then narrower. | You can drag the edge to make it wider or narrower depending on what you need. |
| Press F1 again (or click the close button) to close the sidebar. | And press F1 again to close it. |
| Hold. | Any time you're in the app and you're not sure what something does, hit F1 first. The answer is probably in there. |

---

## 12. Wrap-Up

| On Screen | Voiceover |
|---|---|
| Return to the Progress view, full window visible. | And that's the tour. |
| Hold. | At this point you should be able to install VANTAGE, log in, and find your way around the main window. You know what every menu does, what every navigation button leads to, where to find your settings, and how to get help. |
| Hold. | The next step is to start watching the module-specific videos. The Progress video is a great place to start — that's where you'll spend most of your time. From there you can move on to Schedule, Progress Books, Work Packages, Analysis, and Takeoffs as you need them. |
| Hold. | Quick reminders before we wrap up. **F1** opens the help sidebar from anywhere — use it. The **Feedback Board** in the Settings menu is the best way to report a bug or suggest a new feature. And the app updates itself, so you don't have to worry about it. |
| Final hold on the main window. End card or fade. | Thanks for watching, and welcome to VANTAGE: Milestone. |
