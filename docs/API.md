# Social-Calc API Documentation

## PDF Export API

The PDF export API supports both synchronous rendering for small files and asynchronous background generation for large files.

### 1. `POST /api/export/pdf`
Generates a PDF from a given spreadsheet ID or JSON data.

**Request Body (JSON):**
```json
{
  "spreadsheetId": 123, // Optional. If omitted, must provide "data".
  "data": { // Optional if spreadsheetId is provided.
    "fileName": "My Sheet",
    "jsonData": "..."
  },
  "printSettings": { // Optional
    "pageSize": "A4",
    "landscape": true,
    "marginTop": "10mm",
    "marginBottom": "10mm",
    "marginLeft": "10mm",
    "marginRight": "10mm",
    "headerTemplate": "<div>...</div>",
    "footerTemplate": "<div>...</div>",
    "scale": 1.0,
    "printBackground": true,
    "watermarkText": "CONFIDENTIAL"
  }
}
```

**Responses:**
- `200 OK` (Content-Type: `application/pdf`): If the spreadsheet is small (< 500KB JSON), the API processes it synchronously and streams the PDF bytes back immediately as an attachment download.
- `202 Accepted` (Content-Type: `application/json`): If the spreadsheet is large (> 500KB JSON), the API queues it in the background to prevent timeouts.
  - Body: `{ "success": true, "jobId": "...", "message": "Job queued. Check status endpoint." }`

---

### 2. `GET /api/export/pdf/status/{jobId}`
Checks the status of an asynchronous background job.

**Responses:**
- `200 OK`
  - Body: `{ "success": true, "jobId": "...", "status": "Pending|Processing|Completed|Failed", "error": "" }`
- `404 Not Found`: If the job ID does not exist.

---

### 3. `GET /api/export/pdf/download/{jobId}`
Downloads the completed PDF from a background job. The temp file is automatically deleted after the download finishes.

**Responses:**
- `200 OK` (Content-Type: `application/pdf`): Streams the file as an attachment download.
- `400 Bad Request`: If the job is not in a `Completed` state.
- `404 Not Found`: If the job ID doesn't exist or the file was already deleted.
