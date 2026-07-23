/**
 * Runs when the Google Sheet is opened.
 * Creates a custom menu in the Google Sheets UI.
 */
function onOpen(e) {
  SpreadsheetApp.getUi()
      .createMenu('SocialCalc')
      .addItem('Export to SocialCalc', 'exportToSocialCalc')
      .addToUi();
}

/**
 * Reads the active sheet's data and sends it to the SocialCalc ASP.NET Core API.
 */
function exportToSocialCalc() {
  var ui = SpreadsheetApp.getUi();
  var sheet = SpreadsheetApp.getActiveSheet();
  var sheetName = sheet.getName();
  
  // Get data range and values
  var dataRange = sheet.getDataRange();
  var values = dataRange.getValues();
  
  if (values.length === 0 || (values.length === 1 && values[0].length === 0 && values[0][0] === "")) {
    ui.alert('Export Failed', 'The spreadsheet is empty.', ui.ButtonSet.OK);
    return;
  }
  
  // Show a toast that export is starting
  SpreadsheetApp.getActiveSpreadsheet().toast('Preparing data for export...', 'Exporting', 3);
  
  // Construct payload
  var payload = {
    sheetName: sheetName,
    data: values,
    rowCount: values.length,
    colCount: values[0].length
  };
  
  try {
    var response = sendToSocialCalcApi(payload);
    
    // Parse response
    var jsonResponse = JSON.parse(response.getContentText());
    
    if (response.getResponseCode() === 200 && jsonResponse.success) {
      ui.alert('Export Successful', 'Spreadsheet successfully exported to SocialCalc!\nID: ' + jsonResponse.id, ui.ButtonSet.OK);
    } else {
      ui.alert('Export Error', 'Failed to export: ' + (jsonResponse.message || 'Unknown error'), ui.ButtonSet.OK);
    }
  } catch (error) {
    ui.alert('Export Error', 'Could not connect to SocialCalc API.\nError: ' + error.toString(), ui.ButtonSet.OK);
  }
}

/**
 * Sends HTTP POST request to the configured API.
 * @param {Object} payload The data to send.
 * @return {GoogleAppsScript.URL_Fetch.HTTPResponse} The API response.
 */
function sendToSocialCalcApi(payload) {
  var url = Config.API_BASE_URL + '/api/gworkspace/import';
  
  var options = {
    'method': 'post',
    'contentType': 'application/json',
    'payload': JSON.stringify(payload),
    'headers': {
      'X-Api-Key': Config.API_KEY
    },
    'muteHttpExceptions': true
  };
  
  return UrlFetchApp.fetch(url, options);
}
