#  Social-Calc Deployment Test Plan

**Test Environment:**
* **URL:** `http://51.20.72.142:5000`
* **Infrastructure:** AWS EC2 (Ubuntu 24.04 LTS), AWS Security Groups, Supabase PostgreSQL (Cloud Database)
* **Demo Credentials:** `demo@example.com` / `DemoPass123`

---

## Part 1: Infrastructure & Core Functionality

### Test Case 1: Infrastructure & Network Accessibility
* **Objective:** Verify the AWS EC2 instance is properly routing external traffic.
* **Steps:** 
  1. Navigate to the URL in an incognito web browser (desktop and mobile).
* **Expected Result:** The application homepage loads successfully within a few seconds without connection refusal, confirming Port 5000 is correctly opened in the AWS Security Group.

### Test Case 2: Authentication & Database Connection
* **Objective:** Verify the application is successfully reading from the Supabase PostgreSQL database.
* **Steps:**
  1. Click "Login".
  2. Enter the demo credentials (`demo@example.com` / `DemoPass123`).
  3. Click Submit.
* **Expected Result:** User is successfully authenticated and redirected to the application dashboard. This confirms the EC2 instance can successfully query the external Supabase connection pooler.

### Test Case 3: User Registration & Data Writing
* **Objective:** Verify the application can successfully write new records to Supabase.
* **Steps:**
  1. Log out of the demo account.
  2. Go to the "Register" page.
  3. Create a brand new account with a dummy email and password.
  4. Log in with the newly created account.
* **Expected Result:** The account is created successfully without database timeout errors. The new user can log in immediately.

### Test Case 4: Core Application Functionality (Spreadsheets)
* **Objective:** Verify the core business logic of creating and saving spreadsheets works end-to-end.
* **Steps:**
  1. While logged in, click to create a "New Sheet".
  2. Type some test data into the spreadsheet cells (e.g., `10` in A1, `20` in A2, `=SUM(A1:A2)` in A3).
  3. Save the sheet.
  4. Refresh the page or navigate away and come back.
* **Expected Result:** The sheet saves successfully to the database. Upon reloading, the data and formulas remain intact.

### Test Case 5: Background Services & Interoperability (PHP Export)(WIP)
* **Objective:** Verify the internal background services (PHP CLI handlers) are configured with correct Linux paths and executing properly.
* **Steps:**
  1. Open a saved spreadsheet.
  2. Click the option to "Export" the file (e.g., to Excel, CSV, or PDF).
* **Expected Result:** The server successfully generates the file and prompts a download. This verifies that the C# application is successfully communicating with the PHP CLI service installed on the Ubuntu server.

---

## Part 2: Advanced Architecture & Resilience

### Test Case 6: Concurrent Editing & Session Management
* **Objective:** Verify the application handles multiple active user sessions without data crossover.
* **Steps:**
  1. Open the application in two entirely different browsers (e.g., Chrome and Firefox).
  2. Log into Account A in Chrome, and Account B in Firefox.
  3. Create "Sheet A" on Account A, and "Sheet B" on Account B.
* **Expected Result:** Neither user can see the other user's sheets on their dashboard. The JWT/Cookie session tokens strictly enforce data separation between the two clients.

### Test Case 7: Invalid Input & Error Handling 
* **Objective:** Verify the application fails gracefully when given bad data instead of crashing the server.
* **Steps:**
  1. Go to the Registration page.
  2. Attempt to register without entering an email, or by entering a password that is too short.
  3. Attempt to register using an email address that already exists in the database.
* **Expected Result:** The application catches the invalid input, rejects the database insertion, and displays a user-friendly error message on the UI. The background `dotnet` process on the server does not crash.

### Test Case 8: Process Resilience (The "Crash Test")
* **Objective:** Verify that the background process manager keeps the application alive.
* **Steps:**
  1. The Architect verifies the site is live at `http://51.20.72.142:5000`.
  2. The Administrator logs into the EC2 instance via SSH.
  3. The Administrator purposefully closes the SSH terminal without stopping the app.
* **Expected Result:** The Architect refreshes the page and verifies the website is still perfectly accessible, proving the application is successfully detached and running as a daemon/background task.

### Test Case 9: Unauthorized Access Simulation
* **Objective:** Verify route protection and authentication middleware.
* **Steps:**
  1. Ensure you are completely logged out of the application.
  2. Attempt to manually navigate directly to a protected URL (e.g., trying to force load the URL for a specific spreadsheet or the dashboard).
* **Expected Result:** The application intercepts the unauthorized request and automatically redirects the user back to the Login screen. 

---

## Part 3: Security, Performance & Monitoring (WIP)

### Test Case 10: Security (SQL Injection & XSS Protection)
* **Objective:** Verify the application and database are protected against malicious input.
* **Steps:**
  1. Create a new spreadsheet.
  2. In one of the cells, type a malicious SQL string (e.g., `' OR 1=1; DROP TABLE Users; --`).
  3. In another cell, type a basic Javascript injection (e.g., `<script>alert('hack');</script>`).
  4. Save the sheet and reload the page.
* **Expected Result:** The Entity Framework Core ORM automatically sanitizes the SQL input, so the database is unharmed. The frontend framework automatically escapes the JavaScript, rendering it as plain text rather than executing it. 

### Test Case 11: Performance & Connection Pooling
* **Objective:** Verify the Supabase connection pooler doesn't bottleneck under moderate load.
* **Steps:**
  1. Rapidly refresh the dashboard page 20-30 times in a row, or use a basic load-testing tool to send 50 simultaneous login requests.
* **Expected Result:** The application remains responsive. Because the application uses Supabase's IPv4 **Session Pooler** (Port 5432), the database handles the rapid connection spikes gracefully without throwing "Too many connections" fatal errors.

### Test Case 12: Logging & Observability
* **Objective:** Verify the server is accurately tracking errors for future debugging.
* **Steps:**
  1. Intentionally trigger a minor error (e.g., trying to export a completely blank sheet to PDF).
  2. SSH into the Amazon EC2 server.
  3. Re-attach to the background process (using `screen -r`) or view the application log files.
* **Expected Result:** The server terminal or log file outputs a timestamped error message detailing exactly what failed (e.g., `Error exporting PDF: Object reference not set to an instance of an object`).

### Test Case 13: Cross-Origin Resource Sharing (CORS) & Allowed Hosts
* **Objective:** Verify that the server rejects requests from spoofed or unauthorized domains.
* **Steps:**
  1. The Architect uses a tool like Postman or `curl` to send a POST request to the API, but manually fakes the `Origin` or `Host` header to pretend the request is coming from a malicious website (e.g., `evil-website.com`).
* **Expected Result:** Depending on how strictly `AllowedHosts` is configured, the server should inspect the request and potentially reject it, preventing Cross-Site Request Forgery (CSRF).
