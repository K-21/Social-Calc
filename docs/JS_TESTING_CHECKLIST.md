# Manual JS Testing Checklist (SheetJS Interoperability)

Because the spreadsheet JavaScript logic (`SocialCalc` + `SheetJS`) is heavily integrated directly into the DOM of the Razor views (`Index.cshtml` and `Editor.cshtml`), automated headless testing is complex. 
Use this manual checklist to thoroughly verify the frontend JS logic.

## Prerequisites
- Start the web application locally.
- Navigate to the Dashboard (`/Sheets`).
- Have the files from the `test-fixtures` directory ready (`sample.csv`, `sample.tsv`).

---

## Part 1: Import Logic (SheetJS Parsing)
*Triggered via the "Import" button on the Dashboard, or the Editor's Import dropdown.*

- [ ] **Basic CSV Import**
  - **Action:** Import `test-fixtures/sample.csv`.
  - **Expected:** The editor opens. The table data appears exactly as in the CSV. Headers (Name, Age, etc.) are properly mapped to row 1.
- [ ] **Basic TSV Import**
  - **Action:** Import `test-fixtures/sample.tsv`.
  - **Expected:** Data parses cleanly into columns without tabs appearing in the text itself.
- [ ] **Excel Import (Multiple Sheets)**
  - **Action:** Import an `.xlsx` file containing multiple tabs.
  - **Expected:** The JSON payload constructed in the JS should capture multiple sheet arrays, though SocialCalc may only render the first one by default.
- [ ] **Merged Cells Handling**
  - **Action:** Import an `.xlsx` file with A1:C1 merged.
  - **Expected:** The SheetJS `ws['!merges']` property is correctly extracted by `Index.cshtml` and sent to the API. When the editor loads, A1 should span 3 columns visually.
- [ ] **Error Boundary: Oversized File**
  - **Action:** Attempt to import a file > 5MB.
  - **Expected:** The JavaScript file-size validation immediately halts the import and displays an alert without sending data to the server.

---

## Part 2: Export Logic (SheetJS Generation)
*Triggered via the "Export -> API Exports" dropdown on the Dashboard.*

- [ ] **Basic Excel API Export**
  - **Action:** Click "Excel via API (.xlsx)" on a populated sheet.
  - **Expected:** An `.xlsx` file downloads. When opened in Excel/Google Sheets, the raw values and basic formulas are intact.
- [ ] **CSV / TSV API Export**
  - **Action:** Click "CSV via API" and "TSV via API".
  - **Expected:** The resulting text files properly delimit columns with commas or tabs.
- [ ] **Formula Preservation**
  - **Action:** Create a sheet with `=SUM(A1:A2)` in A3. Click "Excel via API (.xlsx)".
  - **Expected:** Opening the downloaded `.xlsx` in Excel shows a working formula in A3, not a hardcoded value or static string.
- [ ] **Empty Sheet Export Boundary**
  - **Action:** Export a brand new, completely blank sheet.
  - **Expected:** The system gracefully handles the lack of cells and downloads an empty file without throwing `TypeError: Cannot read properties of undefined` in the JS console.
