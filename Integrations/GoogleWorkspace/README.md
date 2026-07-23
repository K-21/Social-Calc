# SocialCalc Google Workspace Marketplace Add-on

This directory contains all the code and configuration required to deploy the SocialCalc integration to the Google Workspace Marketplace.

## Folder Structure
- `AppsScript/`: The `.gs` files containing the Google Apps Script code.
- `Manifest/`: The `appsscript.json` manifest file outlining scopes and configurations.
- `Documentation/`: API and deployment documentation.

## Deployment Instructions

### Prerequisites
1. You need a Google Cloud Platform (GCP) project.
2. Install `clasp` (Command Line Apps Script Projects): `npm install -g @google/clasp`.
3. Login to clasp: `clasp login`.

### Deploying the Apps Script
1. Go into the Apps Script directory.
2. Create a new standalone Apps Script project or clone an existing one:
   ```bash
   clasp create --type standalone --title "SocialCalc Add-on"
   ```
3. Copy the contents of `AppsScript/` and `Manifest/appsscript.json` into the root of the local clasp project folder.
4. Push the code to Google:
   ```bash
   clasp push
   ```
5. Update `Config.gs` with your live ASP.NET Core API Base URL and secure API key.

### Publishing to Google Workspace Marketplace
1. Navigate to the Google Cloud Console for your GCP Project.
2. Go to **APIs & Services** > **OAuth consent screen** and configure it.
3. Enable the **Google Workspace Marketplace SDK**.
4. Configure the Google Workspace Marketplace SDK with your Apps Script project ID (found in `clasp` settings).
5. Submit for verification and publication.
