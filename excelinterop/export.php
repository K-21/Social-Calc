<?php

require 'vendor/autoload.php'; // Composer autoload

use PhpOffice\PhpSpreadsheet\Spreadsheet;
use PhpOffice\PhpSpreadsheet\IOFactory;

require_once __DIR__ . '/socialcalc.inc';
require_once __DIR__ . '/sheetnode_phpexcel.export.inc';

// Read the tmp file
$fname = $argv[1];
$outfile = $argv[2];
$outfiletype = $argv[3];

$fh = fopen($fname, "r");
$data = fread($fh, filesize($fname));
fclose($fh);

// Debug: log input data length and first 500 chars
error_log("Export input file: $fname");
error_log("Export input data length: " . strlen($data));
error_log("Export input data preview: " . substr($data, 0, 500));

$book = json_decode($data);

// Debug: check if JSON decode succeeded
if ($book === null && json_last_error() !== JSON_ERROR_NONE) {
    error_log("JSON decode error: " . json_last_error_msg());
    error_log("Full data: " . $data);
    exit(1);
}

if (!isset($book->sheetArr)) {
    error_log("ERROR: sheetArr not found in data");
    error_log("Data structure keys: " . print_r(array_keys((array)$book), true));
    exit(1);
}

$sheetarr = $book->sheetArr;
$workbook = new Spreadsheet();

$sindex = 0;
$actualactiveindex = 0;

foreach ($sheetarr as $key => $value) {
    if ($sindex > 0) {
        $workbook->createSheet();
        $workbook->setActiveSheetIndex($sindex);
    }
    if ($key == $book->currentid) {
        $actualactiveindex = $sindex;
    }

    $title = $value->name;
    $sheet = socialcalc_parse_sheet($value->sheetstr->savestr);

    _sheetnode_phpexcel_export_do($workbook, $title, $sheet);

    $sindex++;
    echo $sindex . ' done' . PHP_EOL;
}

$workbook->setActiveSheetIndex($actualactiveindex);

// Write the workbook into a file
$objWriter = IOFactory::createWriter($workbook, $outfiletype);
$objWriter->save($outfile);

?>