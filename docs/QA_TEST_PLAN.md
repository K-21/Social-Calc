# SocialCalc Integration Test Plan

This document outlines the testing procedures to validate the **PhpSpreadsheet PDF generation** and legacy integration features.

## Prerequisites
- **Branch:** Ensure you are testing on the `main` branch.
- **Environment:** Run the application locally (`dotnet run`) or deploy to a staging server.
- **PHP Dependencies:** Ensure you have run `composer install` inside the `excelinterop` directory so that `mpdf` is properly installed.

---

## 1. Local Browser Export
*Objective: Verify that spreadsheets can be exported to standard formats.*

### Steps:
1. Navigate to the **My Sheets** dashboard or open any spreadsheet in the **Editor**.
2. Add some test data (text, numbers, basic formulas) to a few cells.
3. Open the **Export** dropdown menu.
4. Click **Excel (.xlsx)** or **CSV (.csv)**.
5. **Verify:** A file should download and exactly match the data in your spreadsheet.

---

## 2. PDF Export & Full-Width Formatting (PhpSpreadsheet + mPDF)
*Objective: Verify that spreadsheets generate PDFs through the PHP CLI backend and that the tables dynamically stretch to fill the horizontal width of an A4 Landscape page without leaving empty whitespace on the right side.*

### Steps:
1. Open a spreadsheet in the **Editor**.
2. Enter a row of data that spans a few columns (e.g., Columns A through C). Ensure it does NOT naturally fill the entire width of the screen.
3. Click the **PDF Document (.pdf)** button from the **Export** dropdown on the dashboard, or the **PDF** button on the editor toolbar.
4. **Verify Generation:** The browser should trigger a download for `{SheetName}.pdf`.
5. Open the downloaded PDF. 
6. **Verify Layout & Width:** 
   - The document orientation should be **Landscape**.
   - The table should dynamically stretch to 100% of the page width.
   - The right margin should be small (~10mm) and completely uniform with the left margin. There should be NO massive block of empty whitespace on the right side of the page.
   - Text inside the cells should wrap cleanly.

---

## 3. Server Logging and Process Execution
*Objective: Verify that the PHP CLI process is spawning correctly and handling the mPDF conversion.*

### Steps:
1. Trigger a PDF export.
2. Monitor the `.NET` application console logs.
3. **Verify:** You should see logs similar to:
   - `Export: Writing data for sheet {SheetName}. Length={size}`
   - `Sheet exported to Pdf (PHP CLI): {SheetName}`
4. If you see `PHP export failed for User...`, verify that `composer install` was executed in the `excelinterop` directory.

---

## 4. Temporary File Cleanup Validation
*Objective: Ensure temporary artifacts are cleaned up by the C# application layer after a successful export.*

### Steps:
1. Trigger a PDF export.
2. While the PDF is generating, check the `excelinterop/tmp` directory. You should see a `.b` (JSON data) file and a `.pdf` file temporarily appear.
3. Once the download finishes, check the directory again.
4. **Verify:** The `.b` and `.pdf` files should have been immediately deleted by the `PhpCliExcelService.TryDelete()` method.
