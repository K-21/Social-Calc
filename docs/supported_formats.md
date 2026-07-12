# Supported Formats and Features

This document outlines the supported formats, features, and known limitations of the SocialCalc to Excel interop pipeline powered by PhpSpreadsheet.

## Supported File Formats

The following file formats are fully supported for both importing and exporting:
- **XLSX**: Microsoft Excel 2007+ Open XML format. (Recommended)
- **XLS**: Legacy Microsoft Excel 97-2003 format.
- **CSV**: Comma-Separated Values. (Lossless for plain data, strips formatting).
- **ODS**: OpenDocument Spreadsheet (LibreOffice/OpenOffice).

## Preserved Styles and Features

When converting between SocialCalc and the above rich formats (XLSX, XLS, ODS), the following features are actively preserved:

### Text and Font Formatting
- **Font Family**: (e.g., Arial, Calibri)
- **Font Size**: Preserved accurately.
- **Bold / Italic**: Persisted across round-trips.
- **Text Color**: Foreground font color definitions.
- **Background Color**: Cell fill colors.

### Cell and Sheet Properties
- **Cell Merging**: Multi-row and multi-column merged areas (`colspan` / `rowspan`).
- **Alignment**: Left, Center, Right, Top, Middle, Bottom alignments.
- **Formulas**: Simple numerical and cell-referencing formulas.
- **Number Formats**: Standard numerical formats including Currency (`$`), Percentages (`%`), and Dates (`dt`).

## Security Features

- **Size Limits**: Hard 5MB limit enforced at both frontend and backend boundaries.
- **File Validation**: Strict allowlist for file extensions (`.xlsx`, `.xls`, `.csv`, `.ods`).
- **CSV Injection Protection**: Strings starting with `=`, `+`, `-`, or `@` are safely quoted on export to prevent malicious execution in desktop spreadsheet clients.
- **Temporary File Sanitization**: All uploads are randomized using cryptographically secure GUIDs and instantly purged post-processing to block path traversal and disk-filling attacks.

## Known Limitations

Due to differences between web-based spreadsheet paradigms and native Excel applications, the following features are **not supported**:
- VBA Macros and Active-X controls (stripped for security reasons).
- Complex Charts and embedded objects/images.
- Pivot Tables.
- Password-protected or encrypted Excel files.
- Multiple-sheet export into a single file (currently exports the active sheet only).
