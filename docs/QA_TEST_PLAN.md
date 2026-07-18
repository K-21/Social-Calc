# SocialCalc Integration Test Plan

This document outlines the testing procedures to validate the newly integrated **SheetJS frontend exports**, **Playwright PDF generation**, **Background Jobs**, and **Google Drive uploads**. 

## Prerequisites
- **Branch:** Ensure you are testing on `feature/sheetjs-integration`.
- **Environment:** Run the application locally (`dotnet run`) or deploy to a staging server.
- **Config:** Ensure your `appsettings.Development.json` has valid Google OAuth Credentials (`ClientId` and `ClientSecret`) to test the Google Drive upload flow.

---

## 1. Local Browser Export (SheetJS)
*Objective: Verify that spreadsheets can be exported instantly to the local machine without server-side processing.*

### Steps:
1. Navigate to the **My Sheets** dashboard or open any spreadsheet in the **Editor**.
2. Add some test data (text, numbers, basic formulas) to a few cells.
3. Open the **Export** dropdown menu.
4. Click **Excel via API (.xlsx)**.
5. **Verify:** A file named `{SheetName}.xlsx` should download immediately.
6. Open the downloaded Excel file and verify that the data, numbers, and basic formatting appear exactly as they did in the browser.
7. Repeat the process for **CSV via API (.csv)** and **TSV via API (.tsv)** to ensure standard text formats download correctly.

---

## 2. PDF Export & Formatting (Playwright)
*Objective: Verify that small spreadsheets generate PDFs instantly, and the formatting is correct.*

### Steps:
1. Open a spreadsheet in the **Editor**.
2. Enter a row of data that spans horizontally (e.g., Columns A through F).
3. Click the **Export PDF** button in the top toolbar.
4. **Verify:** A "Generating PDF..." loading spinner should appear briefly, followed by the browser downloading the `{SheetName}.pdf` file.
5. Open the downloaded PDF. 
6. **Verify Layout:** 
   - The table should span the full width of the page without huge white margins on the right.
   - Long text inside cells should wrap cleanly instead of blowing out the column width.
   - You should see the faint "CONFIDENTIAL" watermark running diagonally across the page.

---

## 3. Asynchronous Background Queues
*Objective: Verify that large spreadsheets trigger the background worker queue so the main thread doesn't hang.*

### Steps:
1. Create a **massive** spreadsheet (or use the **Import** feature to upload a large `.xlsx` file). Ensure the data size will exceed 500KB (e.g., 5,000+ rows).
2. Click **Export PDF**.
3. **Verify UI Polling:** Instead of instantly downloading, the UI should show a background polling message (e.g., "PDF Processing...").
4. Let it run. Every 2 seconds, the client will poll the backend. 
5. **Verify Completion:** Once the background worker finishes the render, the UI should swap to a "Saved!" indicator and automatically download the PDF.
6. Check the server console logs. You should see entries indicating:
   `PDF Background Worker started.`
   `Dequeued job for spreadsheet {id}`
   `Job {id} completed successfully.`

---

## 4. Google Drive Integration (SheetJS -> API)
*Objective: Verify that the SheetJS engine can generate an Excel blob and push it to Google Drive.*

### Steps:
1. Open a spreadsheet in the **Editor**.
2. Click the **Export to Google Drive** button.
3. **Auth Check:** If you are not logged into Google, it should briefly redirect you to the Google Login/Consent screen.
4. **Verify UI:** A spinner indicating "Uploading to Drive..." should appear next to the sheet name.
5. **Verify Delivery:** Once the upload succeeds, a new browser tab should automatically open, pointing directly to the live file inside your Google Drive account.
6. Look at the file in Google Drive to verify the contents match your spreadsheet.

---

## 5. File Cleanup (Security & Maintenance)
*Objective: Ensure temporary artifacts are not hoarding disk space.*

### Steps:
1. Trigger an asynchronous PDF export (Step 3).
2. Once the file downloads successfully, leave the server running.
3. Check the `excelinterop/tmp` directory in the project folder.
4. **Verify:** After roughly 5-10 minutes, the background `TempCleanupService` should automatically sweep and delete any old `.pdf`, `.html`, or `.b` files that are no longer needed.
