# OpsHub static demo

This folder is a standalone, static GitHub Pages demo. It does not run the ASP.NET Core app, connect to SQLite, or provide authentication or server-side authorization. It is populated with fictional sample tickets.

Ticket creation, editing, status changes, and deletion are demo-only. Changes are saved in the current browser's `localStorage`; they are not shared with other visitors and must not contain real or sensitive information. Use **Reset sample data** to restore the initial examples.

## Publish with GitHub Pages

1. Push this repository to GitHub.
2. In the repository, open **Settings → Pages** and set **Source** to **GitHub Actions**.
3. Push to `main` (or manually run the **Deploy static OpsHub demo** workflow from the Actions tab).
4. GitHub Pages will publish the `docs/` folder. For this repository, the project site URL is `https://zrzndn.github.io/DotNet/`.

The workflow deploys only this static demo, not the ASP.NET Core application.

For the full application with accounts, permissions, and persistent ticket storage, use an ASP.NET Core-capable host and a production database.
